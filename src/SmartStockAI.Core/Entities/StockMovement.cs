using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Entities;

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DocumentNumber { get; set; }

    public Product Product { get; set; } = null!;
}
