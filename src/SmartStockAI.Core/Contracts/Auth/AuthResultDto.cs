using SmartStockAI.Core.Contracts.Users;

namespace SmartStockAI.Core.Contracts.Auth;

public sealed class AuthResultDto
{
    public bool IsAuthenticated { get; init; }
    public string? Error { get; init; }
    public UserDto? User { get; init; }
}
