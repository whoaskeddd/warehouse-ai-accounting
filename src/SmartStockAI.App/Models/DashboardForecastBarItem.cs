namespace SmartStockAI.App.Models;

public sealed class DashboardForecastBarItem
{
    public string Label { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public double Height { get; init; }
    public bool IsForecast { get; init; }
}
