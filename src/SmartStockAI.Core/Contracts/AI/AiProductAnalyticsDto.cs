namespace SmartStockAI.Core.Contracts.AI;

public sealed class AiProductAnalyticsDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public DateTime? ForecastCalculatedAtUtc { get; init; }
    public decimal AverageMonthlyDemand { get; init; }
    public decimal ForecastLeadTime { get; init; }
    public decimal SafetyStock { get; init; }
    public decimal ExpectedInbound { get; init; }
    public decimal RecommendedOrder { get; init; }
    public decimal ProjectedDeficit { get; init; }
    public bool UsesFallback { get; init; }
    public IReadOnlyList<AiForecastPointDto> History { get; init; } = [];
    public IReadOnlyList<AiForecastPointDto> Forecast { get; init; } = [];
}
