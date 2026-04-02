namespace SmartStockAI.Core.Contracts.Stock;

public sealed class UpdateStockDocumentRequest
{
    public int? SupplierId { get; init; }
    public string? Comment { get; init; }
    public IReadOnlyList<SaveStockDocumentLineRequest> Lines { get; init; } = [];
}
