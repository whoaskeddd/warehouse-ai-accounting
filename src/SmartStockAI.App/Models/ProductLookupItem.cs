namespace SmartStockAI.App.Models;

public sealed class ProductLookupItem
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = "шт";
    public decimal AvailableStock { get; init; }

    public string DisplayName => $"{Sku} · {Name}";
}
