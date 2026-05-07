namespace SmartStockAI.Core.Entities;

public class ExpectedInboundSnapshot
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CalculatedAtUtc { get; set; }

    public Product Product { get; set; } = null!;
}
