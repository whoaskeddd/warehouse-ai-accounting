namespace SmartStockAI.Core.Contracts.AI;

public sealed class AiCategoryRecommendationDto
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public bool IsStrongRecommendation { get; init; }
    public DateTime? TrainedAtUtc { get; init; }
}
