using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class ProductService(
    AppDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditLogWriter auditLogWriter) : IProductService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        return await dbContext.Products
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(ToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

        return await dbContext.Products
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

        var normalizedSku = request.Sku.Trim();
        var normalizedName = request.Name.Trim();
        var normalizedUnit = request.Unit.Trim();

        ValidateRequiredStrings(normalizedSku, normalizedName, normalizedUnit, nameof(request));
        await ValidateForeignKeysAsync(request.CategoryId, request.SupplierId, request.LocationId, cancellationToken);

        var skuExists = await dbContext.Products
            .AnyAsync(x => x.Sku == normalizedSku, cancellationToken);

        if (skuExists)
        {
            throw new InvalidOperationException($"Product with SKU '{normalizedSku}' already exists.");
        }

        var entity = new Product
        {
            Sku = normalizedSku,
            Name = normalizedName,
            CategoryId = request.CategoryId,
            SupplierId = request.SupplierId,
            LocationId = request.LocationId,
            Unit = normalizedUnit,
            CurrentStock = request.CurrentStock,
            ReservedStock = 0,
            MinStock = request.MinStock,
            PurchasePrice = request.PurchasePrice,
            SalePrice = request.SalePrice
        };

        dbContext.Products.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("Product.Created", nameof(Product), entity.Id.ToString(), $"Product '{entity.Sku}' created.", cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<ProductDto?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

        var entity = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var normalizedSku = request.Sku.Trim();
        var normalizedName = request.Name.Trim();
        var normalizedUnit = request.Unit.Trim();

        ValidateRequiredStrings(normalizedSku, normalizedName, normalizedUnit, nameof(request));
        await ValidateForeignKeysAsync(request.CategoryId, request.SupplierId, request.LocationId, cancellationToken);

        var skuExists = await dbContext.Products
            .AnyAsync(x => x.Id != id && x.Sku == normalizedSku, cancellationToken);

        if (skuExists)
        {
            throw new InvalidOperationException($"Product with SKU '{normalizedSku}' already exists.");
        }

        entity.Sku = normalizedSku;
        entity.Name = normalizedName;
        entity.CategoryId = request.CategoryId;
        entity.SupplierId = request.SupplierId;
        entity.LocationId = request.LocationId;
        entity.Unit = normalizedUnit;
        entity.CurrentStock = request.CurrentStock;
        entity.MinStock = request.MinStock;
        entity.PurchasePrice = request.PurchasePrice;
        entity.SalePrice = request.SalePrice;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("Product.Updated", nameof(Product), entity.Id.ToString(), $"Product '{entity.Sku}' updated.", cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

        var entity = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var hasMovements = await dbContext.StockMovements.AnyAsync(x => x.ProductId == id, cancellationToken);
        if (hasMovements)
        {
            throw new InvalidOperationException("Cannot delete product that already has stock movements.");
        }

        var hasDraftDocuments = await dbContext.StockDocumentLines.AnyAsync(x => x.ProductId == id, cancellationToken);
        if (hasDraftDocuments)
        {
            throw new InvalidOperationException("Cannot delete product that is used by stock documents.");
        }

        var hasReservations = await dbContext.StockReservations.AnyAsync(x => x.ProductId == id && !x.IsReleased, cancellationToken);
        if (hasReservations)
        {
            throw new InvalidOperationException("Cannot delete product that has active reservations.");
        }

        dbContext.Products.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("Product.Deleted", nameof(Product), id.ToString(), $"Product '{entity.Sku}' deleted.", cancellationToken);

        return true;
    }

    private void ValidateRequiredStrings(string sku, string name, string unit, string paramName)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("Product SKU is required.", paramName);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", paramName);
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Product unit is required.", paramName);
        }
    }

    private async Task ValidateForeignKeysAsync(int? categoryId, int? supplierId, int? locationId, CancellationToken cancellationToken)
    {
        if (categoryId.HasValue)
        {
            var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == categoryId.Value, cancellationToken);
            if (!categoryExists)
            {
                throw new InvalidOperationException($"Category '{categoryId.Value}' was not found.");
            }
        }

        if (supplierId.HasValue)
        {
            var supplierExists = await dbContext.Suppliers.AnyAsync(x => x.Id == supplierId.Value, cancellationToken);
            if (!supplierExists)
            {
                throw new InvalidOperationException($"Supplier '{supplierId.Value}' was not found.");
            }
        }

        if (locationId.HasValue)
        {
            var locationExists = await dbContext.Locations.AnyAsync(x => x.Id == locationId.Value, cancellationToken);
            if (!locationExists)
            {
                throw new InvalidOperationException($"Location '{locationId.Value}' was not found.");
            }
        }
    }

    private static System.Linq.Expressions.Expression<Func<Product, ProductDto>> ToDtoExpression() =>
        product => new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            CategoryId = product.CategoryId,
            CategoryName = product.Category != null ? product.Category.Name : null,
            SupplierId = product.SupplierId,
            SupplierName = product.Supplier != null ? product.Supplier.Name : null,
            LocationId = product.LocationId,
            LocationName = product.Location != null ? product.Location.Name : null,
            Unit = product.Unit,
            CurrentStock = product.CurrentStock,
            ReservedStock = product.ReservedStock,
            AvailableStock = product.CurrentStock - product.ReservedStock,
            MinStock = product.MinStock,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice
        };
}
