using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Contracts.Stock;

public sealed class StockDocumentDto
{
    public int Id { get; init; }
    public string Number { get; init; } = string.Empty;
    public StockDocumentType Type { get; init; }
    public StockDocumentStatus Status { get; init; }
    public int? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PostedAt { get; init; }
    public decimal TotalQuantity { get; init; }
    public int TotalItems { get; init; }
    public IReadOnlyList<StockDocumentLineDto> Lines { get; init; } = [];
}
