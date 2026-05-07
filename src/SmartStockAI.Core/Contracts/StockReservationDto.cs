namespace SmartStockAI.Core.Contracts.Stock;

public sealed class StockReservationDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductSku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public bool IsReleased { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReleasedAt { get; init; }
}
