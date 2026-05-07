namespace SmartStockAI.Core.Contracts.Reports;

public sealed class ReportDefinitionDto
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public override string ToString() => Name;
}
