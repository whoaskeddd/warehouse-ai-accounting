namespace SmartStockAI.App.Models;

public sealed class DocumentLineItem
{
    public int LineNo { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = "шт";
    public decimal Quantity { get; set; }
    public decimal AvailableStock { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool HasShortage { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
}
