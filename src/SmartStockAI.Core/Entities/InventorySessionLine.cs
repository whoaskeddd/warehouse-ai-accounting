namespace SmartStockAI.Core.Entities;

public class InventorySessionLine
{
    public int Id { get; set; }
    public int InventorySessionId { get; set; }
    public int ProductId { get; set; }
    public decimal ExpectedStock { get; set; }
    public decimal? ActualStock { get; set; }
    public decimal? Variance { get; set; }
    public string? Comment { get; set; }

    public InventorySession InventorySession { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
