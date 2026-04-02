namespace SmartStockAI.Core.Contracts.Audit;

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
