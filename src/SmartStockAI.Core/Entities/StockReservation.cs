namespace SmartStockAI.Core.Entities;

public class StockReservation
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public bool IsReleased { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    public Product Product { get; set; } = null!;
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
