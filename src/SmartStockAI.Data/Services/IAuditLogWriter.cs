namespace SmartStockAI.Data.Services;

public interface IAuditLogWriter
{
    Task WriteAsync(string actionType, string entityType, string entityId, string details, CancellationToken cancellationToken = default);
}
