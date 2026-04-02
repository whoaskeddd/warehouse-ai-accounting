using SmartStockAI.Core.Enums;

namespace SmartStockAI.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<InventorySession> StartedInventorySessions { get; set; } = new List<InventorySession>();
    public ICollection<InventorySession> CompletedInventorySessions { get; set; } = new List<InventorySession>();
    public ICollection<BackupEntry> CreatedBackups { get; set; } = new List<BackupEntry>();
    public ICollection<BackupEntry> RestoredBackups { get; set; } = new List<BackupEntry>();
}
