namespace SmartStockAI.Core.Contracts.Categories;

public sealed class CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public int? ParentCategoryId { get; init; }
}
