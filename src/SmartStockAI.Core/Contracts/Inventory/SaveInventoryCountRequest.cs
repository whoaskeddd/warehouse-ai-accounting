namespace SmartStockAI.Core.Contracts.Inventory;

public sealed class SaveInventoryCountRequest
{
    public int ProductId { get; init; }
    public decimal ActualStock { get; init; }
    public string? Comment { get; init; }
}
