using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Locations;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;

namespace SmartStockAI.Data.Services;

public sealed class LocationService(AppDbContext dbContext) : ILocationService
{
    public async Task<IReadOnlyList<LocationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Locations
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(ToDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<LocationDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(ToDtoExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LocationDto> CreateAsync(CreateLocationRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Location name is required.", nameof(request));
        }

        await ValidateParentAsync(request.ParentLocationId, null, cancellationToken);

        var entity = new Location
        {
            Name = normalizedName,
            ParentLocationId = request.ParentLocationId
        };

        dbContext.Locations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<LocationDto?> UpdateAsync(int id, UpdateLocationRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Locations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Location name is required.", nameof(request));
        }

        await ValidateParentAsync(request.ParentLocationId, id, cancellationToken);

        entity.Name = normalizedName;
        entity.ParentLocationId = request.ParentLocationId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Locations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var hasChildren = await dbContext.Locations.AnyAsync(x => x.ParentLocationId == id, cancellationToken);
        if (hasChildren)
        {
            throw new InvalidOperationException("Cannot delete location that has child locations.");
        }

        var hasProducts = await dbContext.Products.AnyAsync(x => x.LocationId == id, cancellationToken);
        if (hasProducts)
        {
            throw new InvalidOperationException("Cannot delete location that is used by products.");
        }

        dbContext.Locations.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task ValidateParentAsync(int? parentLocationId, int? currentLocationId, CancellationToken cancellationToken)
    {
        if (!parentLocationId.HasValue)
        {
            return;
        }

        if (currentLocationId.HasValue && parentLocationId.Value == currentLocationId.Value)
        {
            throw new InvalidOperationException("Location cannot reference itself as parent.");
        }

        var parentExists = await dbContext.Locations.AnyAsync(x => x.Id == parentLocationId.Value, cancellationToken);
        if (!parentExists)
        {
            throw new InvalidOperationException($"Parent location '{parentLocationId.Value}' was not found.");
        }
    }

    private static System.Linq.Expressions.Expression<Func<Location, LocationDto>> ToDtoExpression() =>
        location => new LocationDto
        {
            Id = location.Id,
            Name = location.Name,
            ParentLocationId = location.ParentLocationId,
            ParentLocationName = location.ParentLocation != null ? location.ParentLocation.Name : null
        };
}
