using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Users;
using SmartStockAI.Core.Entities;
using SmartStockAI.Core.Enums;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class UserService(
    AppDbContext dbContext,
    IPasswordHasher passwordHasher,
    ICurrentUserAccessor currentUserAccessor,
    IAuditLogWriter auditLogWriter) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        return await dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(ToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        return await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserDto?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        var normalizedLogin = login.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLogin))
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Login == normalizedLogin)
            .Select(ToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        var normalizedLogin = request.Login.Trim();

        if (string.IsNullOrWhiteSpace(normalizedLogin))
        {
            throw new ArgumentException("User login is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("User display name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("User password is required.", nameof(request));
        }

        if (request.Role == UserRole.Admin)
        {
            throw new InvalidOperationException("Creating a second admin user is forbidden.");
        }

        var loginExists = await dbContext.Users
            .AnyAsync(x => x.Login == normalizedLogin, cancellationToken);

        if (loginExists)
        {
            throw new InvalidOperationException($"User with login '{normalizedLogin}' already exists.");
        }

        var (hash, salt) = passwordHasher.HashPassword(request.Password);

        var entity = new User
        {
            Login = normalizedLogin,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = request.Role,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Users.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("User.Created", nameof(User), entity.Id.ToString(), $"User '{entity.Login}' created with role '{entity.Role}'.", cancellationToken);

        return ToDto(entity);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        var entity = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var normalizedLogin = request.Login.Trim();

        if (string.IsNullOrWhiteSpace(normalizedLogin))
        {
            throw new ArgumentException("User login is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("User display name is required.", nameof(request));
        }

        if (entity.Role == UserRole.Admin && request.Role != UserRole.Admin)
        {
            throw new InvalidOperationException("The built-in admin role cannot be changed.");
        }

        if (request.Role == UserRole.Admin && entity.Role != UserRole.Admin)
        {
            throw new InvalidOperationException("Creating a second admin user is forbidden.");
        }

        var loginExists = await dbContext.Users
            .AnyAsync(x => x.Id != id && x.Login == normalizedLogin, cancellationToken);

        if (loginExists)
        {
            throw new InvalidOperationException($"User with login '{normalizedLogin}' already exists.");
        }

        entity.Login = normalizedLogin;
        entity.DisplayName = request.DisplayName.Trim();
        entity.Role = request.Role;
        entity.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var (hash, salt) = passwordHasher.HashPassword(request.Password);
            entity.PasswordHash = hash;
            entity.PasswordSalt = salt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("User.Updated", nameof(User), entity.Id.ToString(), $"User '{entity.Login}' updated.", cancellationToken);

        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        var entity = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        if (entity.Role == UserRole.Admin)
        {
            throw new InvalidOperationException("The built-in admin user cannot be deleted.");
        }

        dbContext.Users.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("User.Deleted", nameof(User), entity.Id.ToString(), $"User '{entity.Login}' deleted.", cancellationToken);

        return true;
    }

    private static UserDto ToDto(User user) =>
        new()
        {
            Id = user.Id,
            Login = user.Login,
            DisplayName = user.DisplayName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc,
            LastLoginAtUtc = user.LastLoginAtUtc
        };

    private static System.Linq.Expressions.Expression<Func<User, UserDto>> ToDtoExpression() =>
        user => new UserDto
        {
            Id = user.Id,
            Login = user.Login,
            DisplayName = user.DisplayName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc,
            LastLoginAtUtc = user.LastLoginAtUtc
        };
}
