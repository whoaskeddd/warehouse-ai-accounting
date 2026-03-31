namespace SmartStockAI.Core.Contracts.Locations;

public sealed class CreateLocationRequest
{
    public string Name { get; init; } = string.Empty;
    public int? ParentLocationId { get; init; }
}
