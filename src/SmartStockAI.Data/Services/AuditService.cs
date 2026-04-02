using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Audit;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class AuditService(AppDbContext dbContext, ICurrentUserAccessor currentUserAccessor)
    : IAuditService, IAuditLogWriter
{
    public async Task<IReadOnlyList<AuditLogDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        return await dbContext.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                UserId = x.UserId,
                UserDisplayName = x.User != null ? x.User.DisplayName : null,
                ActionType = x.ActionType,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Details = x.Details,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task WriteAsync(string actionType, string entityType, string entityId, string details, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserAccessor.UserId,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
