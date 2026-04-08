namespace SmartStockAI.Core.Contracts.AI;

public sealed class AiCategoryForecastSummaryDto
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal MonthlyForecast { get; init; }
    public decimal ForecastSixMonths { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
}
