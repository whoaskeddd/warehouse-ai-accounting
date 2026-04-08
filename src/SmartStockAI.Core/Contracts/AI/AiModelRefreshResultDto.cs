namespace SmartStockAI.Core.Contracts.AI;

public sealed class AiModelRefreshResultDto
{
    public DateTime RefreshedAtUtc { get; init; }
    public int ProductForecastCount { get; init; }
    public int CategoryForecastCount { get; init; }
    public int CategoryTrainingRowsCount { get; init; }
    public decimal? CategoryModelScore { get; init; }
    public decimal? AverageForecastError { get; init; }
}
