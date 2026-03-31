using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Suppliers;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;

namespace SmartStockAI.Data.Services;

public sealed class SupplierService(AppDbContext dbContext) : ISupplierService
{
    public async Task<IReadOnlyList<SupplierDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Suppliers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SupplierDto
            {
                Id = x.Id,
                Name = x.Name,
                ContactInfo = x.ContactInfo
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SupplierDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Suppliers
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new SupplierDto
            {
                Id = x.Id,
                Name = x.Name,
                ContactInfo = x.ContactInfo
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Supplier name is required.", nameof(request));
        }

        var entity = new Supplier
        {
            Name = normalizedName,
            ContactInfo = request.ContactInfo?.Trim()
        };

        dbContext.Suppliers.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<SupplierDto?> UpdateAsync(int id, UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Supplier name is required.", nameof(request));
        }

        entity.Name = normalizedName;
        entity.ContactInfo = request.ContactInfo?.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var hasProducts = await dbContext.Products.AnyAsync(x => x.SupplierId == id, cancellationToken);
        if (hasProducts)
        {
            throw new InvalidOperationException("Cannot delete supplier that is used by products.");
        }

        dbContext.Suppliers.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
