namespace SmartStockAI.Core.Contracts.Suppliers;

public sealed class UpdateSupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ContactInfo { get; init; }
}
