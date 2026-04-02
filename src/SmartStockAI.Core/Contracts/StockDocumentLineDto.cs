namespace SmartStockAI.Core.Contracts.Stock;

public sealed class StockDocumentLineDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string? Comment { get; init; }
}
