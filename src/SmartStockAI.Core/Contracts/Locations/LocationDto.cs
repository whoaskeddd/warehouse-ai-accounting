namespace SmartStockAI.Core.Contracts.Locations;

public sealed class LocationDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? ParentLocationId { get; init; }
    public string? ParentLocationName { get; init; }
}
