using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Contracts.Stock;

public sealed class CreateStockDocumentRequest
{
    public string Number { get; init; } = string.Empty;
    public StockDocumentType Type { get; init; }
    public int? SupplierId { get; init; }
    public string? Comment { get; init; }
    public IReadOnlyList<SaveStockDocumentLineRequest> Lines { get; init; } = [];
}
