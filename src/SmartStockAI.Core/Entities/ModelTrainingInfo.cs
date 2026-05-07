namespace SmartStockAI.Core.Entities;

public class ModelTrainingInfo
{
    public int Id { get; set; }
    public string ModelType { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public int? ScopeId { get; set; }
    public DateTime TrainedAtUtc { get; set; }
    public int TrainingRowsCount { get; set; }
    public decimal? QualityMetric { get; set; }
    public string ArtifactPath { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
