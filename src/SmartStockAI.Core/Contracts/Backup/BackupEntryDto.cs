namespace SmartStockAI.Core.Contracts.Backup;

public sealed class BackupEntryDto
{
    public int Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public int CreatedByUserId { get; init; }
    public string CreatedByUserDisplayName { get; init; } = string.Empty;
    public DateTime? RestoredAtUtc { get; init; }
    public int? RestoredByUserId { get; init; }
    public string? RestoredByUserDisplayName { get; init; }
}
