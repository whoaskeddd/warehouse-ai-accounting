namespace SmartStockAI.Core.Entities;

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
