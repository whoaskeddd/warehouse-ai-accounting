namespace SmartStockAI.Core.Entities;

public class StockDocumentLine
{
    public int Id { get; set; }
    public int StockDocumentId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Comment { get; set; }

    public StockDocument StockDocument { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
