namespace SmartStockAI.Core.Contracts.Stock;

public sealed class SaveStockDocumentLineRequest
{
    public int ProductId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string? Comment { get; init; }
}
