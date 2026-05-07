namespace SmartStockAI.Core.Contracts.AI;

public interface IAiService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<AiModelRefreshResultDto> RefreshModelsAsync(CancellationToken cancellationToken = default);
    Task<AiDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<AiProductAnalyticsDto?> GetProductAnalyticsAsync(int productId, CancellationToken cancellationToken = default);
    Task<AiCategoryRecommendationDto?> SuggestCategoryAsync(string productName, CancellationToken cancellationToken = default);
}
