using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Entities;

public class StockDocument
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public StockDocumentType Type { get; set; }
    public StockDocumentStatus Status { get; set; }
    public int? SupplierId { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PostedAt { get; set; }

    public Supplier? Supplier { get; set; }
    public ICollection<StockDocumentLine> Lines { get; set; } = new List<StockDocumentLine>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
