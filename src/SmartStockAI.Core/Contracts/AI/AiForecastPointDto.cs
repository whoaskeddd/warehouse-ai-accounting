namespace SmartStockAI.Core.Contracts.AI;

public sealed class AiForecastPointDto
{
    public DateOnly Period { get; init; }
    public decimal Quantity { get; init; }
    public decimal LowerBound { get; init; }
    public decimal UpperBound { get; init; }
    public bool IsForecast { get; init; }
}
