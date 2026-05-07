using SmartStockAI.Core.Enums;

namespace SmartStockAI.Data.Security;

public interface ICurrentUserAccessor
{
    int? UserId { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    void SetCurrentUser(int userId, UserRole role);
    void Clear();
}
