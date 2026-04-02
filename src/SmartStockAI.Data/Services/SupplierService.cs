using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Suppliers;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class SupplierService(
    AppDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditLogWriter auditLogWriter) : ISupplierService
{
    public async Task<IReadOnlyList<SupplierDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

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
        AuthorizationGuard.EnsureAuthenticated(currentUserAccessor);

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
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

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
        await auditLogWriter.WriteAsync("Supplier.Created", nameof(Supplier), entity.Id.ToString(), $"Supplier '{entity.Name}' created.", cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<SupplierDto?> UpdateAsync(int id, UpdateSupplierRequest request, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

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
        await auditLogWriter.WriteAsync("Supplier.Updated", nameof(Supplier), entity.Id.ToString(), $"Supplier '{entity.Name}' updated.", cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

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
        await auditLogWriter.WriteAsync("Supplier.Deleted", nameof(Supplier), id.ToString(), $"Supplier '{entity.Name}' deleted.", cancellationToken);

        return true;
    }
}
