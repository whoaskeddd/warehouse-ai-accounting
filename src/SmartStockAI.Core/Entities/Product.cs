namespace SmartStockAI.Core.Entities;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public int? SupplierId { get; set; }
    public int? LocationId { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal ReservedStock { get; set; }
    public decimal MinStock { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }

    public Category? Category { get; set; }
    public Supplier? Supplier { get; set; }
    public Location? Location { get; set; }
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<StockDocumentLine> StockDocumentLines { get; set; } = new List<StockDocumentLine>();
    public ICollection<StockReservation> StockReservations { get; set; } = new List<StockReservation>();
}
