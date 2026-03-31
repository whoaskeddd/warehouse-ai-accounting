using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Users;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;

namespace SmartStockAI.Data.Services;

public sealed class UserService(AppDbContext dbContext) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .Select(ToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedLogin = request.Login.Trim();

        if (string.IsNullOrWhiteSpace(normalizedLogin))
        {
            throw new ArgumentException("User login is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("User display name is required.", nameof(request));
        }

        var loginExists = await dbContext.Users
            .AnyAsync(x => x.Login == normalizedLogin, cancellationToken);

        if (loginExists)
        {
            throw new InvalidOperationException($"User with login '{normalizedLogin}' already exists.");
        }

        var entity = new User
        {
            Login = normalizedLogin,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role
        };

        dbContext.Users.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
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

        var loginExists = await dbContext.Users
            .AnyAsync(x => x.Id != id && x.Login == normalizedLogin, cancellationToken);

        if (loginExists)
        {
            throw new InvalidOperationException($"User with login '{normalizedLogin}' already exists.");
        }

        entity.Login = normalizedLogin;
        entity.DisplayName = request.DisplayName.Trim();
        entity.Role = request.Role;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.Users.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static UserDto ToDto(User user) =>
        new()
        {
            Id = user.Id,
            Login = user.Login,
            DisplayName = user.DisplayName,
            Role = user.Role
        };

    private static System.Linq.Expressions.Expression<Func<User, UserDto>> ToDtoExpression() =>
        user => new UserDto
        {
            Id = user.Id,
            Login = user.Login,
            DisplayName = user.DisplayName,
            Role = user.Role
        };
}
