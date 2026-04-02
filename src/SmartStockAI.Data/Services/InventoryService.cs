using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Inventory;
using SmartStockAI.Core.Entities;
using SmartStockAI.Core.Enums;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class InventoryService(
    AppDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditLogWriter auditLogWriter) : IInventoryService
{
    public async Task<IReadOnlyList<InventorySessionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

        var sessions = await dbContext.InventorySessions
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Include(x => x.CompletedByUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Include(x => x.DiscrepancyReport)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return sessions.Select(MapSession).ToList();
    }

    public async Task<InventorySessionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

        var entity = await LoadSessionAsync(id, cancellationToken);
        return entity is null ? null : MapSession(entity);
    }

    public async Task<InventorySessionDto> CreateAsync(CreateInventorySessionRequest request, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

        var number = request.Number.Trim();
        if (string.IsNullOrWhiteSpace(number))
        {
            throw new ArgumentException("Inventory session number is required.", nameof(request));
        }

        var exists = await dbContext.InventorySessions.AnyAsync(x => x.Number == number, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"Inventory session '{number}' already exists.");
        }

        var currentUserId = currentUserAccessor.UserId ?? throw new InvalidOperationException("Authentication is required.");
        var entity = new InventorySession
        {
            Number = number,
            Status = InventorySessionStatus.Draft,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedByUserId = currentUserId,
            StartedAtUtc = DateTime.UtcNow
        };

        dbContext.InventorySessions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("Inventory.Created", nameof(InventorySession), entity.Id.ToString(), $"Inventory session '{entity.Number}' created.", cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<InventorySessionDto?> SaveCountAsync(int sessionId, SaveInventoryCountRequest request, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

        var session = await dbContext.InventorySessions
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        EnsureDraft(session);

        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken);
        if (product is null)
        {
            throw new InvalidOperationException($"Product '{request.ProductId}' was not found.");
        }

        var line = session.Lines.FirstOrDefault(x => x.ProductId == request.ProductId);
        var variance = request.ActualStock - product.CurrentStock;

        if (line is null)
        {
            line = new InventorySessionLine
            {
                ProductId = product.Id,
                ExpectedStock = product.CurrentStock,
                ActualStock = request.ActualStock,
                Variance = variance,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim()
            };

            session.Lines.Add(line);
        }
        else
        {
            line.ExpectedStock = product.CurrentStock;
            line.ActualStock = request.ActualStock;
            line.Variance = variance;
            line.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(sessionId, cancellationToken);
    }

    public async Task<InventorySessionDto?> CompleteAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureWarehouseOrAdmin(currentUserAccessor);

        var session = await dbContext.InventorySessions
            .Include(x => x.Lines)
            .ThenInclude(x => x.Product)
            .Include(x => x.DiscrepancyReport)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        EnsureDraft(session);

        if (session.Lines.Count == 0 || session.Lines.Any(x => !x.ActualStock.HasValue))
        {
            throw new InvalidOperationException("Inventory session must contain counted lines before completion.");
        }

        foreach (var line in session.Lines)
        {
            var variance = line.ActualStock!.Value - line.ExpectedStock;
            line.Variance = variance;
            line.Product.CurrentStock = line.ActualStock.Value;

            if (variance != 0)
            {
                dbContext.StockMovements.Add(new StockMovement
                {
                    ProductId = line.ProductId,
                    Type = StockMovementType.Adjustment,
                    Quantity = variance,
                    BalanceAfter = line.Product.CurrentStock - line.Product.ReservedStock,
                    CreatedAt = DateTime.UtcNow,
                    DocumentNumber = session.Number,
                    Comment = $"Inventory completion for session '{session.Number}'."
                });
            }
        }

        session.Status = InventorySessionStatus.Completed;
        session.CompletedAtUtc = DateTime.UtcNow;
        session.CompletedByUserId = currentUserAccessor.UserId;

        var report = session.DiscrepancyReport ?? new DiscrepancyReport
        {
            Number = $"DISC-{session.Number}",
            CreatedAtUtc = DateTime.UtcNow
        };

        report.TotalItems = session.Lines.Count(x => x.Variance.HasValue && x.Variance.Value != 0);
        report.TotalVariance = session.Lines.Where(x => x.Variance.HasValue).Sum(x => Math.Abs(x.Variance!.Value));
        session.DiscrepancyReport = report;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("Inventory.Completed", nameof(InventorySession), session.Id.ToString(), $"Inventory session '{session.Number}' completed.", cancellationToken);

        return await GetByIdAsync(sessionId, cancellationToken);
    }

    private async Task<InventorySession?> LoadSessionAsync(int id, CancellationToken cancellationToken)
    {
        return await dbContext.InventorySessions
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Include(x => x.CompletedByUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Product)
            .Include(x => x.DiscrepancyReport)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private static void EnsureDraft(InventorySession session)
    {
        if (session.Status != InventorySessionStatus.Draft)
        {
            throw new InvalidOperationException("Only draft inventory sessions can be modified.");
        }
    }

    private static InventorySessionDto MapSession(InventorySession session)
    {
        return new InventorySessionDto
        {
            Id = session.Id,
            Number = session.Number,
            Status = session.Status,
            Comment = session.Comment,
            CreatedByUserId = session.CreatedByUserId,
            CreatedByUserDisplayName = session.CreatedByUser.DisplayName,
            CompletedByUserId = session.CompletedByUserId,
            CompletedByUserDisplayName = session.CompletedByUser?.DisplayName,
            StartedAtUtc = session.StartedAtUtc,
            CompletedAtUtc = session.CompletedAtUtc,
            Lines = session.Lines
                .OrderBy(x => x.Id)
                .Select(x => new InventorySessionLineDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    ProductSku = x.Product.Sku,
                    ProductName = x.Product.Name,
                    ExpectedStock = x.ExpectedStock,
                    ActualStock = x.ActualStock,
                    Variance = x.Variance,
                    Comment = x.Comment
                })
                .ToList(),
            DiscrepancyReport = session.DiscrepancyReport is null
                ? null
                : new DiscrepancyReportDto
                {
                    Id = session.DiscrepancyReport.Id,
                    Number = session.DiscrepancyReport.Number,
                    CreatedAtUtc = session.DiscrepancyReport.CreatedAtUtc,
                    TotalItems = session.DiscrepancyReport.TotalItems,
                    TotalVariance = session.DiscrepancyReport.TotalVariance
                }
        };
    }
}
