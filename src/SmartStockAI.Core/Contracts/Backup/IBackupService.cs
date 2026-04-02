namespace SmartStockAI.Core.Contracts.Backup;

public interface IBackupService
{
    Task<IReadOnlyList<BackupEntryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BackupEntryDto> CreateBackupAsync(CancellationToken cancellationToken = default);
    Task<BackupEntryDto?> RestoreBackupAsync(int backupEntryId, CancellationToken cancellationToken = default);
}
