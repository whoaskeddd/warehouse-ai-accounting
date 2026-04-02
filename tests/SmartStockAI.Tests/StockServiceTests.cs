using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Stock;
using SmartStockAI.Core.Entities;
using SmartStockAI.Core.Enums;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Services;

namespace SmartStockAI.Tests;

public class StockServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public StockServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task PostReceiptDocument_ShouldIncreaseCurrentStock_AndCreateMovement()
    {
        await using var context = CreateContext();
        var product = await SeedProductAsync(context, currentStock: 10m);
        var service = new StockService(context);

        var document = await service.CreateDocumentAsync(new CreateStockDocumentRequest
        {
            Number = "IN-0001",
            Type = StockDocumentType.Receipt,
            Lines =
            [
                new SaveStockDocumentLineRequest
                {
                    ProductId = product.Id,
                    Quantity = 5m,
                    UnitPrice = 120m,
                    Comment = "Receipt"
                }
            ]
        });

        var posted = await service.PostDocumentAsync(document.Id);

        posted.Should().NotBeNull();
        posted!.Status.Should().Be(StockDocumentStatus.Posted);

        var updatedProduct = await context.Products.SingleAsync(x => x.Id == product.Id);
        updatedProduct.CurrentStock.Should().Be(15m);
        updatedProduct.ReservedStock.Should().Be(0m);

        var movement = await context.StockMovements.SingleAsync();
        movement.Type.Should().Be(StockMovementType.Receipt);
        movement.Quantity.Should().Be(5m);
        movement.BalanceAfter.Should().Be(15m);
        movement.DocumentNumber.Should().Be("IN-0001");
    }

    [Fact]
    public async Task PostIssueDocument_ShouldThrow_WhenAvailableStockIsLowerThanRequested()
    {
        await using var context = CreateContext();
        var product = await SeedProductAsync(context, currentStock: 10m);
        var service = new StockService(context);

        await service.CreateReservationAsync(new CreateStockReservationRequest
        {
            ProductId = product.Id,
            Quantity = 4m,
            Reference = "ORD-001"
        });

        var document = await service.CreateDocumentAsync(new CreateStockDocumentRequest
        {
            Number = "OUT-0001",
            Type = StockDocumentType.Issue,
            Lines =
            [
                new SaveStockDocumentLineRequest
                {
                    ProductId = product.Id,
                    Quantity = 7m,
                    UnitPrice = 0m
                }
            ]
        });

        var action = async () => await service.PostDocumentAsync(document.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");

        var updatedProduct = await context.Products.SingleAsync(x => x.Id == product.Id);
        updatedProduct.CurrentStock.Should().Be(10m);
        updatedProduct.ReservedStock.Should().Be(4m);
        context.StockMovements.Should().ContainSingle(x => x.Type == StockMovementType.Reservation);
        context.StockMovements.Should().NotContain(x => x.Type == StockMovementType.Issue);
    }

    [Fact]
    public async Task ReleaseReservation_ShouldFreeAvailableStock_ForIssuePosting()
    {
        await using var context = CreateContext();
        var product = await SeedProductAsync(context, currentStock: 10m);
        var service = new StockService(context);

        var reservation = await service.CreateReservationAsync(new CreateStockReservationRequest
        {
            ProductId = product.Id,
            Quantity = 4m,
            Reference = "ORD-002"
        });

        await service.ReleaseReservationAsync(reservation.Id);

        var document = await service.CreateDocumentAsync(new CreateStockDocumentRequest
        {
            Number = "OUT-0002",
            Type = StockDocumentType.Issue,
            Lines =
            [
                new SaveStockDocumentLineRequest
                {
                    ProductId = product.Id,
                    Quantity = 7m,
                    UnitPrice = 0m,
                    Comment = "Issue"
                }
            ]
        });

        var posted = await service.PostDocumentAsync(document.Id);

        posted.Should().NotBeNull();
        posted!.Status.Should().Be(StockDocumentStatus.Posted);

        var updatedProduct = await context.Products.SingleAsync(x => x.Id == product.Id);
        updatedProduct.CurrentStock.Should().Be(3m);
        updatedProduct.ReservedStock.Should().Be(0m);

        var movements = await context.StockMovements.OrderBy(x => x.Id).ToListAsync();
        movements.Select(x => x.Type).Should().ContainInOrder(
            StockMovementType.Reservation,
            StockMovementType.ReservationRelease,
            StockMovementType.Issue);
        movements.Last().BalanceAfter.Should().Be(3m);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private AppDbContext CreateContext() => new(_options);

    private static async Task<Product> SeedProductAsync(AppDbContext context, decimal currentStock)
    {
        var product = new Product
        {
            Sku = $"SKU-{Guid.NewGuid():N}"[..12],
            Name = "Test product",
            Unit = "pcs",
            CurrentStock = currentStock,
            ReservedStock = 0m,
            MinStock = 1m,
            PurchasePrice = 10m,
            SalePrice = 12m
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }
}
