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

        if (admins.Count == 1)
        {
            return;
        }

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

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
