namespace SmartStockAI.Core.Contracts.AI;

public sealed class AiDashboardDto
{
    public DateTime? LastForecastCalculatedAtUtc { get; init; }
    public IReadOnlyList<AiCriticalStockItemDto> CriticalItems { get; init; } = [];
    public IReadOnlyList<AiPurchaseRecommendationDto> PurchaseRecommendations { get; init; } = [];
    public IReadOnlyList<AiCategoryForecastSummaryDto> CategoryForecasts { get; init; } = [];
}
