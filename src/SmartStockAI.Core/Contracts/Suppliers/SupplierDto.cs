namespace SmartStockAI.Core.Contracts.Suppliers;

public sealed class SupplierDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ContactInfo { get; init; }
}
