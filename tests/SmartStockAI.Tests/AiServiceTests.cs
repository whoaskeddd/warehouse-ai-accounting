using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartStockAI.Core.Entities;
using SmartStockAI.Core.Enums;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;
using SmartStockAI.Data.Services;
using SmartStockAI.Data.Services.Ai;

namespace SmartStockAI.Tests;

public class AiServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly string _modelDirectory;

    public AiServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _modelDirectory = Path.Combine(AppContext.BaseDirectory, "test-models", Guid.NewGuid().ToString("N"));

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task RefreshModelsAsync_ShouldBuildForecasts_AndSuggestCategory()
    {
        await using var context = CreateContext();
        var laptopCategory = await SeedCategoryAsync(context, "Ноутбуки");
        var mouseCategory = await SeedCategoryAsync(context, "Мыши");

        var laptop = await SeedProductAsync(context, "NB-100", "Ноутбук Lenovo ThinkPad", laptopCategory.Id, 2m);
        var mouse = await SeedProductAsync(context, "MS-200", "Мышь Logitech Wireless", mouseCategory.Id, 10m);
        var newLaptop = await SeedProductAsync(context, "NB-NEW", "Ноутбук Lenovo Basic", laptopCategory.Id, 1m);

        await SeedIssueHistoryAsync(context, laptop.Id, [4m, 5m, 6m, 5m]);
        await SeedIssueHistoryAsync(context, mouse.Id, [2m, 2m, 3m, 2m]);
        await SeedIssueHistoryAsync(context, newLaptop.Id, [1m, 1m]);
        await SeedDraftInboundAsync(context, newLaptop.Id, 2m);

        var service = CreateAiService(context);

        var refresh = await service.RefreshModelsAsync();
        var dashboard = await service.GetDashboardAsync();
        var analytics = await service.GetProductAnalyticsAsync(newLaptop.Id);
        var suggestion = await service.SuggestCategoryAsync("Lenovo office notebook");

        refresh.ProductForecastCount.Should().Be(3);
        refresh.CategoryForecastCount.Should().BeGreaterThanOrEqualTo(2);
        dashboard.PurchaseRecommendations.Should().NotBeEmpty();
        analytics.Should().NotBeNull();
        analytics!.Forecast.Should().HaveCountGreaterThanOrEqualTo(6);
        analytics.UsesFallback.Should().BeTrue();
        analytics.ExpectedInbound.Should().Be(2m);
        suggestion.Should().NotBeNull();
        suggestion!.CategoryName.Should().Be("Ноутбуки");
        context.ForecastSnapshots.Should().NotBeEmpty();
        context.ModelTrainingInfos.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _connection.Dispose();

        if (Directory.Exists(_modelDirectory))
        {
            Directory.Delete(_modelDirectory, recursive: true);
        }
    }

    private AppDbContext CreateContext() => new(_options);

    private AiService CreateAiService(AppDbContext context)
    {
        var currentUserAccessor = new CurrentUserAccessor();
        currentUserAccessor.SetCurrentUser(1, UserRole.WarehouseOperator);

        var options = Options.Create(new AiOptions
        {
            LeadTimeMonths = 1,
            SafetyStockMonths = 1,
            MinimumHistoryMonthsForForecast = 3,
            StrongCategoryRecommendationThreshold = 0.75m,
            ModelDirectoryName = _modelDirectory
        });

        return new AiService(context, currentUserAccessor, options);
    }

    private static async Task<Category> SeedCategoryAsync(AppDbContext context, string name)
    {
        var category = new Category { Name = name };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    private static async Task<Product> SeedProductAsync(AppDbContext context, string sku, string name, int categoryId, decimal currentStock)
    {
        var product = new Product
        {
            Sku = sku,
            Name = name,
            CategoryId = categoryId,
            Unit = "pcs",
            CurrentStock = currentStock,
            MinStock = 3m,
            PurchasePrice = 10m,
            SalePrice = 20m
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private static async Task SeedIssueHistoryAsync(AppDbContext context, int productId, decimal[] monthlyValues)
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < monthlyValues.Length; i++)
        {
            context.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                Type = StockMovementType.Issue,
                Quantity = monthlyValues[i],
                BalanceAfter = 0m,
                CreatedAt = start.AddMonths(i).AddDays(3),
                DocumentNumber = $"OUT-{productId}-{i + 1}"
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedDraftInboundAsync(AppDbContext context, int productId, decimal quantity)
    {
        var document = new StockDocument
        {
            Number = $"IN-{productId}",
            Type = StockDocumentType.Receipt,
            Status = StockDocumentStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            Lines =
            [
                new StockDocumentLine
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = 10m
                }
            ]
        };

        context.StockDocuments.Add(document);
        await context.SaveChangesAsync();
    }
}
