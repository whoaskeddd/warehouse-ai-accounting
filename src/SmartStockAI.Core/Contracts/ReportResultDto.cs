namespace SmartStockAI.Core.Contracts.Reports;

public sealed class ReportResultDto
{
    public string ReportKey { get; init; } = string.Empty;
    public string ReportName { get; init; } = string.Empty;
    public DateTime GeneratedAtUtc { get; init; }
    public string Summary { get; init; } = string.Empty;
    public List<ReportColumnDto> Columns { get; init; } = [];
    public List<Dictionary<string, string>> Rows { get; init; } = [];
}
