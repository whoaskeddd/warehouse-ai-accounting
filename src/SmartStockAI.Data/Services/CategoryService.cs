using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Categories;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;

namespace SmartStockAI.Data.Services;

public sealed class CategoryService(AppDbContext dbContext) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(ToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Category name is required.", nameof(request));
        }

        await ValidateParentAsync(request.ParentCategoryId, null, cancellationToken);

        var entity = new Category
        {
            Name = normalizedName,
            ParentCategoryId = request.ParentCategoryId
        };

        dbContext.Categories.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<CategoryDto?> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Category name is required.", nameof(request));
        }

        await ValidateParentAsync(request.ParentCategoryId, id, cancellationToken);

        entity.Name = normalizedName;
        entity.ParentCategoryId = request.ParentCategoryId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var hasChildren = await dbContext.Categories.AnyAsync(x => x.ParentCategoryId == id, cancellationToken);
        if (hasChildren)
        {
            throw new InvalidOperationException("Cannot delete category that has child categories.");
        }

        var hasProducts = await dbContext.Products.AnyAsync(x => x.CategoryId == id, cancellationToken);
        if (hasProducts)
        {
            throw new InvalidOperationException("Cannot delete category that is used by products.");
        }

        dbContext.Categories.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task ValidateParentAsync(int? parentCategoryId, int? currentCategoryId, CancellationToken cancellationToken)
    {
        if (!parentCategoryId.HasValue)
        {
            return;
        }

        if (currentCategoryId.HasValue && parentCategoryId.Value == currentCategoryId.Value)
        {
            throw new InvalidOperationException("Category cannot reference itself as parent.");
        }

        var parentExists = await dbContext.Categories.AnyAsync(x => x.Id == parentCategoryId.Value, cancellationToken);
        if (!parentExists)
        {
            throw new InvalidOperationException($"Parent category '{parentCategoryId.Value}' was not found.");
        }
    }

    private static System.Linq.Expressions.Expression<Func<Category, CategoryDto>> ToDtoExpression() =>
        category => new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            ParentCategoryId = category.ParentCategoryId,
            ParentCategoryName = category.ParentCategory != null ? category.ParentCategory.Name : null
        };
}
