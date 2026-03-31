namespace SmartStockAI.Core.Contracts.Suppliers;

public sealed class CreateSupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ContactInfo { get; init; }
}
