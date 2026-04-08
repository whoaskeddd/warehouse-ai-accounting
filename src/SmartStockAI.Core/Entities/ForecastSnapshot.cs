namespace SmartStockAI.Core.Entities;

public class ForecastSnapshot
{
    public int Id { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public int ScopeId { get; set; }
    public string ScopeName { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public int HistoryMonthsCount { get; set; }
    public bool UsesFallback { get; set; }
    public string? SourceScopeType { get; set; }
    public int? SourceScopeId { get; set; }
    public string? SourceScopeName { get; set; }
    public decimal AverageMonthlyDemand { get; set; }
    public decimal ForecastLeadTime { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal ExpectedInbound { get; set; }
    public decimal RecommendedOrder { get; set; }
    public decimal ProjectedDeficit { get; set; }
    public decimal? ModelQuality { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string ArtifactPath { get; set; } = string.Empty;
}
