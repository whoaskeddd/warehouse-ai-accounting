namespace SmartStockAI.App.Models;

public sealed class CategoryListItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ParentName { get; init; } = "Корень";
    public int ProductCount { get; init; }
}
