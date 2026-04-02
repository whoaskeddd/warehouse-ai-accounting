namespace SmartStockAI.App.Models;

public sealed class MovementHistoryItem
{
    public DateTime OccurredAt { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string MovementType { get; init; } = string.Empty;
    public string DocumentNumber { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal BalanceAfter { get; init; }
    public string Comment { get; init; } = string.Empty;
}
