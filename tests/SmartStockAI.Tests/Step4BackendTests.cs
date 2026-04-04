using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Auth;
using SmartStockAI.Core.Contracts.Inventory;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Reports;
using SmartStockAI.Core.Contracts.Users;
using SmartStockAI.Core.Entities;
using SmartStockAI.Core.Enums;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;
using SmartStockAI.Data.Services;

namespace SmartStockAI.Tests;

public class Step4BackendTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public Step4BackendTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AppDataInitializer_And_AuthService_ShouldSeedAndLoginHardcodedAdmin()
    {
        await using var context = CreateContext();
        var currentUserAccessor = new CurrentUserAccessor();
        var passwordHasher = new Pbkdf2PasswordHasher();
        var auditService = new AuditService(context, currentUserAccessor);
        var initializer = new AppDataInitializer(context, passwordHasher);

        await initializer.InitializeAsync();

        var authService = new AuthService(context, currentUserAccessor, passwordHasher, auditService);
        var result = await authService.LoginAsync(new LoginRequest
        {
            Login = DefaultAdminCredentials.Login,
            Password = DefaultAdminCredentials.Password
        });

        result.IsAuthenticated.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.Role.Should().Be(UserRole.Admin);
        currentUserAccessor.UserId.Should().Be(result.User.Id);
    }

    [Fact]
    public async Task AppDataInitializer_ShouldSynchronizeExistingAdminOnEveryStartup()
    {
        await using var context = CreateContext();
        var passwordHasher = new Pbkdf2PasswordHasher();
        var (oldHash, oldSalt) = passwordHasher.HashPassword("OldPassword123!");

        context.Users.Add(new User
        {
            Login = "legacy-admin",
            DisplayName = "Legacy Admin",
            PasswordHash = oldHash,
            PasswordSalt = oldSalt,
            Role = UserRole.Admin,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        });
        await context.SaveChangesAsync();

        var initializer = new AppDataInitializer(context, passwordHasher);
        await initializer.InitializeAsync();

        var admin = await context.Users.SingleAsync(x => x.Role == UserRole.Admin);
        admin.Login.Should().Be(DefaultAdminCredentials.Login);
        admin.DisplayName.Should().Be(DefaultAdminCredentials.DisplayName);
        admin.IsActive.Should().BeTrue();
        passwordHasher.Verify(DefaultAdminCredentials.Password, admin.PasswordHash, admin.PasswordSalt).Should().BeTrue();
    }

    [Fact]
    public async Task UserService_ShouldAllowAdminToCreateManager_AndPreventSecondAdmin()
    {
        await using var context = CreateContext();
        var currentUserAccessor = await SeedAndLoginAdminAsync(context);
        var auditService = new AuditService(context, currentUserAccessor);
        var userService = new UserService(context, new Pbkdf2PasswordHasher(), currentUserAccessor, auditService);

        var created = await userService.CreateAsync(new CreateUserRequest
        {
            Login = "manager1",
            DisplayName = "Manager 1",
            Password = "Manager123!",
            Role = UserRole.Manager
        });

        created.Role.Should().Be(UserRole.Manager);

        var action = async () => await userService.CreateAsync(new CreateUserRequest
        {
            Login = "admin2",
            DisplayName = "Admin 2",
            Password = "Admin123!",
            Role = UserRole.Admin
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*second admin*");
    }

    [Fact]
    public async Task ProductService_Create_ShouldWriteAuditLog()
    {
        await using var context = CreateContext();
        var currentUserAccessor = new CurrentUserAccessor();
        currentUserAccessor.SetCurrentUser(7, UserRole.WarehouseOperator);

        context.Users.Add(new User
        {
            Id = 7,
            Login = "warehouse1",
            DisplayName = "Warehouse 1",
            PasswordHash = "x",
            PasswordSalt = "y",
            Role = UserRole.WarehouseOperator,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var auditService = new AuditService(context, currentUserAccessor);
        var productService = new ProductService(context, currentUserAccessor, auditService);

        var product = await productService.CreateAsync(new CreateProductRequest
        {
            Sku = "SKU-AUDIT",
            Name = "Audit Product",
            Unit = "pcs",
            CurrentStock = 2,
            MinStock = 1,
            PurchasePrice = 10,
            SalePrice = 12
        });

        product.Sku.Should().Be("SKU-AUDIT");
        context.AuditLogs.Should().ContainSingle(x => x.ActionType == "Product.Created" && x.EntityId == product.Id.ToString());
    }

    [Fact]
    public async Task InventoryService_Complete_ShouldAdjustStock_AndCreateDiscrepancyReport()
    {
        await using var context = CreateContext();
        var currentUserAccessor = new CurrentUserAccessor();
        currentUserAccessor.SetCurrentUser(10, UserRole.WarehouseOperator);

        context.Users.Add(new User
        {
            Id = 10,
            Login = "warehouse2",
            DisplayName = "Warehouse 2",
            PasswordHash = "x",
            PasswordSalt = "y",
            Role = UserRole.WarehouseOperator,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        var product = new Product
        {
            Sku = "SKU-INV",
            Name = "Inventory Product",
            Unit = "pcs",
            CurrentStock = 10,
            ReservedStock = 0,
            MinStock = 1,
            PurchasePrice = 5,
            SalePrice = 8
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var auditService = new AuditService(context, currentUserAccessor);
        var inventoryService = new InventoryService(context, currentUserAccessor, auditService);

        var session = await inventoryService.CreateAsync(new CreateInventorySessionRequest
        {
            Number = "INV-001"
        });

        await inventoryService.SaveCountAsync(session.Id, new SaveInventoryCountRequest
        {
            ProductId = product.Id,
            ActualStock = 7,
            Comment = "Counted manually"
        });

        var completed = await inventoryService.CompleteAsync(session.Id);

        completed.Should().NotBeNull();
        completed!.Status.Should().Be(InventorySessionStatus.Completed);
        completed.DiscrepancyReport.Should().NotBeNull();
        completed.DiscrepancyReport!.TotalItems.Should().Be(1);
        completed.DiscrepancyReport.TotalVariance.Should().Be(3);

        var updatedProduct = await context.Products.SingleAsync(x => x.Id == product.Id);
        updatedProduct.CurrentStock.Should().Be(7);
        context.StockMovements.Should().ContainSingle(x => x.Type == StockMovementType.Adjustment && x.Quantity == -3);
    }

    [Fact]
    public async Task BackupService_CreateBackup_ShouldCreateFileAndMetadata()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"smartstock-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "backup-test.db");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using (var context = new AppDbContext(options))
            {
                await context.Database.EnsureCreatedAsync();

                var hasher = new Pbkdf2PasswordHasher();
                var currentUserAccessor = new CurrentUserAccessor();
                var initializer = new AppDataInitializer(context, hasher);
                await initializer.InitializeAsync();

                var admin = await context.Users.SingleAsync(x => x.Role == UserRole.Admin);
                currentUserAccessor.SetCurrentUser(admin.Id, admin.Role);

                var auditService = new AuditService(context, currentUserAccessor);
                var backupService = new BackupService(context, currentUserAccessor, auditService);

                var backup = await backupService.CreateBackupAsync();

                File.Exists(backup.FullPath).Should().BeTrue();
                backup.FileName.Should().EndWith(".db");
                context.BackupEntries.Should().ContainSingle(x => x.Id == backup.Id);
            }
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                try
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task ReportService_ShouldExportAndImportExcelSnapshot()
    {
        await using var context = CreateContext();
        var currentUserAccessor = new CurrentUserAccessor();
        currentUserAccessor.SetCurrentUser(21, UserRole.Manager);

        context.Users.Add(new User
        {
            Id = 21,
            Login = "manager-report",
            DisplayName = "Manager Report",
            PasswordHash = "x",
            PasswordSalt = "y",
            Role = UserRole.Manager,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        context.Products.Add(new Product
        {
            Sku = "SKU-REP",
            Name = "Report Product",
            Unit = "pcs",
            CurrentStock = 15,
            ReservedStock = 3,
            MinStock = 5,
            PurchasePrice = 10,
            SalePrice = 14
        });
        await context.SaveChangesAsync();

        var auditService = new AuditService(context, currentUserAccessor);
        var reportService = new ReportService(context, currentUserAccessor, auditService);

        var exported = await reportService.ExportReportToExcelAsync("inventory-balance");
        exported.Should().NotBeEmpty();

        var imported = await reportService.ImportReportFromExcelAsync(exported, "inventory-balance.xlsx");
        imported.ReportKey.Should().Be("inventory-balance");
        imported.RowsCount.Should().Be(1);
        imported.ImportedByDisplayName.Should().Be("Manager Report");

        var stored = await reportService.GetImportedReportAsync(imported.Id);
        stored.Should().NotBeNull();
        stored!.Rows.Should().ContainSingle();
        stored.Rows[0]["sku"].Should().Be("SKU-REP");
        stored.Rows[0]["availableStock"].Should().Be("12");
        context.ImportedReportSnapshots.Should().ContainSingle(x => x.Id == imported.Id);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private AppDbContext CreateContext() => new(_options);

    private static async Task<CurrentUserAccessor> SeedAndLoginAdminAsync(AppDbContext context)
    {
        var hasher = new Pbkdf2PasswordHasher();
        var initializer = new AppDataInitializer(context, hasher);
        await initializer.InitializeAsync();

        var admin = await context.Users.SingleAsync(x => x.Role == UserRole.Admin);
        var currentUserAccessor = new CurrentUserAccessor();
        currentUserAccessor.SetCurrentUser(admin.Id, admin.Role);
        return currentUserAccessor;
    }
}
