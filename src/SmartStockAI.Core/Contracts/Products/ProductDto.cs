namespace SmartStockAI.Core.Contracts.Products;

public sealed class ProductDto
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public int? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public int? LocationId { get; init; }
    public string? LocationName { get; init; }
    public string Unit { get; init; } = string.Empty;
    public decimal CurrentStock { get; init; }
    public decimal MinStock { get; init; }
    public decimal PurchasePrice { get; init; }
    public decimal SalePrice { get; init; }
}
