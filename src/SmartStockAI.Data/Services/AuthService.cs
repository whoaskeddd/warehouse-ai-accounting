using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Auth;
using SmartStockAI.Core.Contracts.Users;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class AuthService(
    AppDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IPasswordHasher passwordHasher,
    IAuditLogWriter auditLogWriter) : IAuthService
{
    public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedLogin = request.Login.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLogin) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthResultDto
            {
                IsAuthenticated = false,
                Error = "Login and password are required."
            };
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Login == normalizedLogin, cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return new AuthResultDto
            {
                IsAuthenticated = false,
                Error = "Invalid login or password."
            };
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        currentUserAccessor.SetCurrentUser(user.Id, user.Role);
        await auditLogWriter.WriteAsync("Auth.Login", nameof(Core.Entities.User), user.Id.ToString(), $"User '{user.Login}' logged in.", cancellationToken);

        return new AuthResultDto
        {
            IsAuthenticated = true,
            User = new UserDto
            {
                Id = user.Id,
                Login = user.Login,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAtUtc = user.CreatedAtUtc,
                LastLoginAtUtc = user.LastLoginAtUtc
            }
        };
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (currentUserAccessor.UserId.HasValue)
        {
            await auditLogWriter.WriteAsync("Auth.Logout", nameof(Core.Entities.User), currentUserAccessor.UserId.Value.ToString(), "User logged out.", cancellationToken);
        }

        currentUserAccessor.Clear();
    }

    public async Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUserAccessor.UserId.HasValue)
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == currentUserAccessor.UserId.Value)
            .Select(x => new UserDto
            {
                Id = x.Id,
                Login = x.Login,
                DisplayName = x.DisplayName,
                Role = x.Role,
                IsActive = x.IsActive,
                CreatedAtUtc = x.CreatedAtUtc,
                LastLoginAtUtc = x.LastLoginAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
