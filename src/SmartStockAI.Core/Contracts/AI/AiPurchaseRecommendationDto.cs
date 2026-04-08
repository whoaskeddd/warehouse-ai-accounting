namespace SmartStockAI.Core.Contracts.AI;

public sealed class AiPurchaseRecommendationDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public decimal CurrentStock { get; init; }
    public decimal ExpectedInbound { get; init; }
    public decimal MonthlyForecast { get; init; }
    public decimal ForecastLeadTime { get; init; }
    public decimal SafetyStock { get; init; }
    public decimal RecommendedOrder { get; init; }
    public bool UsesFallback { get; init; }
}
