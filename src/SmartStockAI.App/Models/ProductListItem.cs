namespace SmartStockAI.App.Models;

public sealed class ProductListItem
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CategoryName { get; init; } = "Без категории";
    public string SupplierName { get; init; } = "Без поставщика";
    public string LocationName { get; init; } = "Без локации";
    public decimal CurrentStock { get; init; }
    public decimal MinStock { get; init; }
    public decimal PurchasePrice { get; init; }
    public decimal SalePrice { get; init; }
}
