namespace SmartStockAI.Core.Contracts.AI;

public sealed class AiCriticalStockItemDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal AvailableStock { get; init; }
    public decimal MinStock { get; init; }
    public decimal MonthlyForecast { get; init; }
    public decimal ProjectedDeficit { get; init; }
    public decimal RecommendedOrder { get; init; }
    public bool UsesFallback { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
}
