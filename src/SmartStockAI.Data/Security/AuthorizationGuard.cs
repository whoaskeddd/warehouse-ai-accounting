using SmartStockAI.Core.Enums;

namespace SmartStockAI.Data.Security;

internal static class AuthorizationGuard
{
    public static void EnsureAuthenticated(ICurrentUserAccessor currentUserAccessor)
    {
        if (!currentUserAccessor.IsAuthenticated)
        {
            throw new InvalidOperationException("Authentication is required.");
        }
    }

    public static void EnsureAdmin(ICurrentUserAccessor currentUserAccessor)
    {
        EnsureRole(currentUserAccessor, UserRole.Admin);
    }

    public static void EnsureWarehouseOrAdmin(ICurrentUserAccessor currentUserAccessor)
    {
        EnsureAnyRole(currentUserAccessor, UserRole.Admin, UserRole.WarehouseOperator);
    }

    public static void EnsureAnyRole(ICurrentUserAccessor currentUserAccessor, params UserRole[] roles)
    {
        EnsureAuthenticated(currentUserAccessor);

        if (currentUserAccessor.Role is null || !roles.Contains(currentUserAccessor.Role.Value))
        {
            throw new InvalidOperationException("Current user is not allowed to perform this operation.");
        }
    }

    public static void EnsureRole(ICurrentUserAccessor currentUserAccessor, UserRole role)
    {
        EnsureAuthenticated(currentUserAccessor);

        if (currentUserAccessor.Role != role)
        {
            throw new InvalidOperationException("Current user is not allowed to perform this operation.");
        }
    }
}
