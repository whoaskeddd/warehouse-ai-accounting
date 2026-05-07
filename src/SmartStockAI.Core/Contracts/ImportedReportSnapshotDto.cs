namespace SmartStockAI.Core.Contracts.Reports;

public sealed class ImportedReportSnapshotDto
{
    public int Id { get; init; }
    public string ReportKey { get; init; } = string.Empty;
    public string ReportName { get; init; } = string.Empty;
    public string SourceFileName { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public DateTime ImportedAtUtc { get; init; }
    public int? ImportedByUserId { get; init; }
    public string ImportedByDisplayName { get; init; } = string.Empty;
    public int RowsCount { get; init; }
    public string Summary { get; init; } = string.Empty;
}
