namespace SmartStockAI.Core.Contracts.Inventory;

public sealed class CreateInventorySessionRequest
{
    public string Number { get; init; } = string.Empty;
    public string? Comment { get; init; }
}
