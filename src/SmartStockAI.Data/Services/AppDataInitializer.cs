using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Entities;
using SmartStockAI.Core.Enums;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class AppDataInitializer(AppDbContext dbContext, IPasswordHasher passwordHasher)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var admins = await dbContext.Users
            .Where(x => x.Role == UserRole.Admin)
            .ToListAsync(cancellationToken);

        if (admins.Count > 1)
        {
            throw new InvalidOperationException("The system must contain exactly one admin user.");
        }

        if (admins.Count == 0)
        {
            var (hash, salt) = passwordHasher.HashPassword(DefaultAdminCredentials.Password);
            dbContext.Users.Add(new User
            {
                Login = DefaultAdminCredentials.Login,
                DisplayName = DefaultAdminCredentials.DisplayName,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            var admin = admins[0];
            var (hash, salt) = passwordHasher.HashPassword(DefaultAdminCredentials.Password);
            admin.Login = DefaultAdminCredentials.Login;
            admin.DisplayName = DefaultAdminCredentials.DisplayName;
            admin.PasswordHash = hash;
            admin.PasswordSalt = salt;
            admin.IsActive = true;
        }

        await EnsureDefaultUserAsync(
            login: "operator",
            displayName: "Warehouse Operator",
            password: "Operator123!",
            role: UserRole.WarehouseOperator,
            cancellationToken);

        await EnsureDefaultUserAsync(
            login: "manager",
            displayName: "Manager",
            password: "Manager123!",
            role: UserRole.Manager,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDefaultUserAsync(
        string login,
        string displayName,
        string password,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Users.AnyAsync(x => x.Login == login, cancellationToken);
        if (exists)
        {
            return;
        }

        var (hash, salt) = passwordHasher.HashPassword(password);
        dbContext.Users.Add(new User
        {
            Login = login,
            DisplayName = displayName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
