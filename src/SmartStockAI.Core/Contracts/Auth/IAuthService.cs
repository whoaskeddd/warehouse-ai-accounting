using SmartStockAI.Core.Contracts.Users;

namespace SmartStockAI.Core.Contracts.Auth;

public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
