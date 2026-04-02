using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Entities;

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? StockDocumentId { get; set; }
    public int? ReservationId { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Comment { get; set; }

    public Product Product { get; set; } = null!;
    public StockDocument? StockDocument { get; set; }
    public StockReservation? Reservation { get; set; }
}
