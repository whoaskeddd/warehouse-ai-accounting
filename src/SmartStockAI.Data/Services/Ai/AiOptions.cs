namespace SmartStockAI.Data.Services.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public decimal LeadTimeMonths { get; init; } = 1m;
    public decimal SafetyStockMonths { get; init; } = 1m;
    public decimal StrongCategoryRecommendationThreshold { get; init; } = 0.75m;
    public int MinimumHistoryMonthsForForecast { get; init; } = 3;
    public string ModelDirectoryName { get; init; } = "models";
}
