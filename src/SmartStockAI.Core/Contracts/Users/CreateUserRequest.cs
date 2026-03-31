using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Contracts.Users;

public sealed class CreateUserRequest
{
    public string Login { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public UserRole Role { get; init; }
}
