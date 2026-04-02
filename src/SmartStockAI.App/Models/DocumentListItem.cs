namespace SmartStockAI.App.Models;

public sealed class DocumentListItem
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public string CounterpartyName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int LinesCount { get; init; }
    public decimal TotalQuantity { get; init; }
    public bool HasWarnings { get; init; }
}
