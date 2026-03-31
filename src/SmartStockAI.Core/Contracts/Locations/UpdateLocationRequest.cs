namespace SmartStockAI.Core.Contracts.Locations;

public sealed class UpdateLocationRequest
{
    public string Name { get; init; } = string.Empty;
    public int? ParentLocationId { get; init; }
}
