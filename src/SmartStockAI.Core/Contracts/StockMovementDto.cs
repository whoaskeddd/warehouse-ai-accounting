using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Contracts.Stock;

public sealed class StockMovementDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int? StockDocumentId { get; init; }
    public int? ReservationId { get; init; }
    public StockMovementType Type { get; init; }
    public decimal Quantity { get; init; }
    public decimal BalanceAfter { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? DocumentNumber { get; init; }
    public string? Comment { get; init; }
}
