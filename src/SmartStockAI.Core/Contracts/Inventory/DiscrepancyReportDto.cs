namespace SmartStockAI.Core.Contracts.Inventory;

public sealed class DiscrepancyReportDto
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public int TotalItems { get; init; }
    public decimal TotalVariance { get; init; }
}
