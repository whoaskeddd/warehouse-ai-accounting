namespace SmartStockAI.Core.Entities;

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? ParentLocationId { get; set; }

    public Location? ParentLocation { get; set; }
    public ICollection<Location> Children { get; set; } = new List<Location>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
