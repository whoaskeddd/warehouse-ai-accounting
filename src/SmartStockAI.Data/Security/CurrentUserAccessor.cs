using SmartStockAI.Core.Enums;

namespace SmartStockAI.Data.Security;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    public int? UserId { get; private set; }
    public UserRole? Role { get; private set; }
    public bool IsAuthenticated => UserId.HasValue && Role.HasValue;

    public void SetCurrentUser(int userId, UserRole role)
    {
        UserId = userId;
        Role = role;
    }

    public void Clear()
    {
        UserId = null;
        Role = null;
    }
}
