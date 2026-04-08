namespace SmartStockAI.Core.Contracts.Products;

public sealed class SearchProductsRequest
{
    public string? SearchText { get; init; }
    public int? CategoryId { get; init; }
    public int? SupplierId { get; init; }
    public bool OnlyCritical { get; init; }
}
