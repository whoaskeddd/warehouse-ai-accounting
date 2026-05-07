namespace SmartStockAI.Core.Entities;

public class DiscrepancyReport
{
    public int Id { get; set; }
    public int InventorySessionId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalVariance { get; set; }

    public InventorySession InventorySession { get; set; } = null!;
}
