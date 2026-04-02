namespace SmartStockAI.Core.Entities;

public class BackupEntry
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? RestoredAtUtc { get; set; }
    public int? RestoredByUserId { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public User? RestoredByUser { get; set; }
}
