namespace SmartStockAI.Core.Contracts.Inventory;

public sealed class InventorySessionLineDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal ExpectedStock { get; init; }
    public decimal? ActualStock { get; init; }
    public decimal? Variance { get; init; }
    public string? Comment { get; init; }
}
