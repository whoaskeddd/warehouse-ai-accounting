namespace SmartStockAI.Core.Entities;

public class ImportedReportSnapshot
{
    public int Id { get; set; }
    public string ReportKey { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    public int? ImportedByUserId { get; set; }
    public string ImportedByDisplayName { get; set; } = string.Empty;
    public int RowsCount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}
