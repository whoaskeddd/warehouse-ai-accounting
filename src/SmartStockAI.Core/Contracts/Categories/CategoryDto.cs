namespace SmartStockAI.Core.Contracts.Categories;

public sealed class CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? ParentCategoryId { get; init; }
    public string? ParentCategoryName { get; init; }
}
