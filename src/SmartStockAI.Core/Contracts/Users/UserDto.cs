using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Contracts.Users;

public sealed class UserDto
{
    public int Id { get; init; }
    public string Login { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
}
