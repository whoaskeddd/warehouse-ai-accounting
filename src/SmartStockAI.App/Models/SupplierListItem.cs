namespace SmartStockAI.App.Models;

public sealed class SupplierListItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ContactInfo { get; init; } = string.Empty;
    public int ProductCount { get; init; }
}
