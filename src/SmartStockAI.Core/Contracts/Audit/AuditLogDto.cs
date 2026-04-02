namespace SmartStockAI.Core.Contracts.Audit;

public sealed class AuditLogDto
{
    public int Id { get; init; }
    public int? UserId { get; init; }
    public string? UserDisplayName { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
