using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Entities;

public class InventorySession
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public InventorySessionStatus Status { get; set; }
    public string? Comment { get; set; }
    public int CreatedByUserId { get; set; }
    public int? CompletedByUserId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public User? CompletedByUser { get; set; }
    public ICollection<InventorySessionLine> Lines { get; set; } = new List<InventorySessionLine>();
    public DiscrepancyReport? DiscrepancyReport { get; set; }
}
