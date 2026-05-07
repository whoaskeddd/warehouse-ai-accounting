namespace SmartStockAI.Core.Contracts.Stock;

public sealed class CreateStockReservationRequest
{
    public int ProductId { get; init; }
    public decimal Quantity { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string? Comment { get; init; }
}
