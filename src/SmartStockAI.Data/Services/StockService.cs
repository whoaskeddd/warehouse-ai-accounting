using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Stock;
using SmartStockAI.Core.Entities;
using SmartStockAI.Core.Enums;
using SmartStockAI.Data.Context;

namespace SmartStockAI.Data.Services;

public sealed class StockService(AppDbContext dbContext) : IStockService
{
    public async Task<IReadOnlyList<StockDocumentDto>> GetDocumentsAsync(StockDocumentType? type = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.StockDocuments
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .AsQueryable();

        if (type.HasValue)
        {
            query = query.Where(x => x.Type == type.Value);
        }

        var documents = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return documents.Select(MapDocument).ToList();
    }

    public async Task<StockDocumentDto?> GetDocumentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StockDocuments
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapDocument(entity);
    }

    public async Task<StockDocumentDto> CreateDocumentAsync(CreateStockDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var number = NormalizeDocumentNumber(request.Number);
        await EnsureDocumentNumberUniqueAsync(number, null, cancellationToken);
        await ValidateSupplierAsync(request.SupplierId, cancellationToken);

        var entity = new StockDocument
        {
            Number = number,
            Type = request.Type,
            Status = StockDocumentStatus.Draft,
            SupplierId = request.SupplierId,
            Comment = NormalizeOptional(request.Comment),
            CreatedAt = DateTime.UtcNow,
            Lines = await CreateLinesAsync(request.Lines, cancellationToken)
        };

        dbContext.StockDocuments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetDocumentByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<StockDocumentDto?> UpdateDocumentAsync(int id, UpdateStockDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StockDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        EnsureDraft(entity);
        await ValidateSupplierAsync(request.SupplierId, cancellationToken);

        dbContext.StockDocumentLines.RemoveRange(entity.Lines);
        entity.Lines = await CreateLinesAsync(request.Lines, cancellationToken);
        entity.SupplierId = request.SupplierId;
        entity.Comment = NormalizeOptional(request.Comment);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetDocumentByIdAsync(id, cancellationToken);
    }

    public async Task<StockDocumentDto?> PostDocumentAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StockDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        EnsureDraft(entity);

        if (entity.Lines.Count == 0)
        {
            throw new InvalidOperationException("Stock document must contain at least one line before posting.");
        }

        var productIds = entity.Lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await dbContext.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var line in entity.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new InvalidOperationException($"Product '{line.ProductId}' was not found.");
            }

            if (entity.Type == StockDocumentType.Receipt)
            {
                product.CurrentStock += line.Quantity;
                dbContext.StockMovements.Add(CreateMovement(product, StockMovementType.Receipt, line.Quantity, entity.Id, null, entity.Number, line.Comment));
                continue;
            }

            var availableStock = product.CurrentStock - product.ReservedStock;
            if (availableStock < line.Quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient stock for product '{product.Sku}'. Available: {availableStock:0.###}, requested: {line.Quantity:0.###}.");
            }

            product.CurrentStock -= line.Quantity;
            dbContext.StockMovements.Add(CreateMovement(product, StockMovementType.Issue, line.Quantity, entity.Id, null, entity.Number, line.Comment));
        }

        entity.Status = StockDocumentStatus.Posted;
        entity.PostedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetDocumentByIdAsync(id, cancellationToken);
    }

    public async Task<StockDocumentDto?> CancelDocumentAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StockDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        EnsureDraft(entity);
        entity.Status = StockDocumentStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetDocumentByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(int? productId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .AsQueryable();

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(MapMovement).ToList();
    }

    public async Task<StockReservationDto> CreateReservationAsync(CreateStockReservationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentException("Reservation quantity must be greater than zero.", nameof(request));
        }

        var reference = NormalizeRequired(request.Reference, nameof(request));
        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"Product '{request.ProductId}' was not found.");

        var availableStock = product.CurrentStock - product.ReservedStock;
        if (availableStock < request.Quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient stock for reservation on product '{product.Sku}'. Available: {availableStock:0.###}, requested: {request.Quantity:0.###}.");
        }

        product.ReservedStock += request.Quantity;

        var reservation = new StockReservation
        {
            ProductId = product.Id,
            Quantity = request.Quantity,
            Reference = reference,
            Comment = NormalizeOptional(request.Comment),
            CreatedAt = DateTime.UtcNow,
            IsReleased = false
        };

        dbContext.StockReservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.StockMovements.Add(CreateMovement(product, StockMovementType.Reservation, request.Quantity, null, reservation.Id, null, reservation.Comment));
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapReservation(reservation, product);
    }

    public async Task<StockReservationDto?> ReleaseReservationAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var reservation = await dbContext.StockReservations
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        if (!reservation.IsReleased)
        {
            reservation.IsReleased = true;
            reservation.ReleasedAt = DateTime.UtcNow;
            reservation.Product.ReservedStock = Math.Max(0, reservation.Product.ReservedStock - reservation.Quantity);
            dbContext.StockMovements.Add(CreateMovement(reservation.Product, StockMovementType.ReservationRelease, reservation.Quantity, null, reservation.Id, null, reservation.Comment));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapReservation(reservation, reservation.Product);
    }

    private async Task<List<StockDocumentLine>> CreateLinesAsync(IReadOnlyList<SaveStockDocumentLineRequest> lines, CancellationToken cancellationToken)
    {
        var normalizedLines = lines
            .Select(x => new SaveStockDocumentLineRequest
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Comment = NormalizeOptional(x.Comment)
            })
            .ToList();

        if (normalizedLines.Any(x => x.Quantity <= 0))
        {
            throw new ArgumentException("Document line quantity must be greater than zero.", nameof(lines));
        }

        var productIds = normalizedLines.Select(x => x.ProductId).Distinct().ToList();
        var existingProductIds = await dbContext.Products
            .Where(x => productIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var missingProductId = productIds.FirstOrDefault(x => !existingProductIds.Contains(x));
        if (productIds.Count != existingProductIds.Count)
        {
            throw new InvalidOperationException($"Product '{missingProductId}' was not found.");
        }

        return normalizedLines
            .Select(x => new StockDocumentLine
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Comment = x.Comment
            })
            .ToList();
    }

    private async Task ValidateSupplierAsync(int? supplierId, CancellationToken cancellationToken)
    {
        if (!supplierId.HasValue)
        {
            return;
        }

        var exists = await dbContext.Suppliers.AnyAsync(x => x.Id == supplierId.Value, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"Supplier '{supplierId.Value}' was not found.");
        }
    }

    private async Task EnsureDocumentNumberUniqueAsync(string number, int? currentDocumentId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.StockDocuments
            .AnyAsync(x => x.Number == number && (!currentDocumentId.HasValue || x.Id != currentDocumentId.Value), cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Stock document '{number}' already exists.");
        }
    }

    private static void EnsureDraft(StockDocument entity)
    {
        if (entity.Status != StockDocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only draft stock documents can be modified.");
        }
    }

    private static string NormalizeDocumentNumber(string number)
    {
        var normalized = number.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Document number is required.", nameof(number));
        }

        return normalized;
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static StockMovement CreateMovement(
        Product product,
        StockMovementType type,
        decimal quantity,
        int? stockDocumentId,
        int? reservationId,
        string? documentNumber,
        string? comment)
    {
        return new StockMovement
        {
            ProductId = product.Id,
            StockDocumentId = stockDocumentId,
            ReservationId = reservationId,
            Type = type,
            Quantity = quantity,
            BalanceAfter = product.CurrentStock - product.ReservedStock,
            CreatedAt = DateTime.UtcNow,
            DocumentNumber = documentNumber,
            Comment = comment
        };
    }

    private static StockDocumentDto MapDocument(StockDocument entity)
    {
        var lines = entity.Lines
            .OrderBy(x => x.Id)
            .Select(x => new StockDocumentLineDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductSku = x.Product?.Sku ?? string.Empty,
                ProductName = x.Product?.Name ?? string.Empty,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                Comment = x.Comment
            })
            .ToList();

        return new StockDocumentDto
        {
            Id = entity.Id,
            Number = entity.Number,
            Type = entity.Type,
            Status = entity.Status,
            SupplierId = entity.SupplierId,
            SupplierName = entity.Supplier?.Name,
            Comment = entity.Comment,
            CreatedAt = entity.CreatedAt,
            PostedAt = entity.PostedAt,
            TotalItems = lines.Count,
            TotalQuantity = lines.Sum(x => x.Quantity),
            Lines = lines
        };
    }

    private static StockMovementDto MapMovement(StockMovement entity)
    {
        return new StockMovementDto
        {
            Id = entity.Id,
            ProductId = entity.ProductId,
            ProductSku = entity.Product.Sku,
            ProductName = entity.Product.Name,
            StockDocumentId = entity.StockDocumentId,
            ReservationId = entity.ReservationId,
            Type = entity.Type,
            Quantity = entity.Quantity,
            BalanceAfter = entity.BalanceAfter,
            CreatedAt = entity.CreatedAt,
            DocumentNumber = entity.DocumentNumber,
            Comment = entity.Comment
        };
    }

    private static StockReservationDto MapReservation(StockReservation entity, Product product)
    {
        return new StockReservationDto
        {
            Id = entity.Id,
            ProductId = entity.ProductId,
            ProductSku = product.Sku,
            ProductName = product.Name,
            Quantity = entity.Quantity,
            Reference = entity.Reference,
            Comment = entity.Comment,
            IsReleased = entity.IsReleased,
            CreatedAt = entity.CreatedAt,
            ReleasedAt = entity.ReleasedAt
        };
    }
}
