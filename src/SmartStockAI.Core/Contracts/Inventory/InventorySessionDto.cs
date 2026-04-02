using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Contracts.Inventory;

public sealed class InventorySessionDto
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public InventorySessionStatus Status { get; init; }
    public string? Comment { get; init; }
    public int CreatedByUserId { get; init; }
    public string CreatedByUserDisplayName { get; init; } = string.Empty;
    public int? CompletedByUserId { get; init; }
    public string? CompletedByUserDisplayName { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public IReadOnlyList<InventorySessionLineDto> Lines { get; init; } = [];
    public DiscrepancyReportDto? DiscrepancyReport { get; init; }
}
