namespace SmartStockAI.App.Models;

public sealed class InventoryCountItem
{
    public int ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal ExpectedStock { get; init; }
    public decimal CountedStock { get; set; }
    public decimal Difference => CountedStock - ExpectedStock;
    public bool HasDifference => Difference != 0;
    public string Comment { get; set; } = string.Empty;
}
