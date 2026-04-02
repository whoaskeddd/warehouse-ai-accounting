using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Contracts.Backup;
using SmartStockAI.Core.Entities;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;

namespace SmartStockAI.Data.Services;

public sealed class BackupService(
    AppDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditLogWriter auditLogWriter) : IBackupService
{
    public async Task<IReadOnlyList<BackupEntryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        return await dbContext.BackupEntries
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Include(x => x.RestoredByUser)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new BackupEntryDto
            {
                Id = x.Id,
                FileName = x.FileName,
                FullPath = x.FullPath,
                CreatedAtUtc = x.CreatedAtUtc,
                CreatedByUserId = x.CreatedByUserId,
                CreatedByUserDisplayName = x.CreatedByUser.DisplayName,
                RestoredAtUtc = x.RestoredAtUtc,
                RestoredByUserId = x.RestoredByUserId,
                RestoredByUserDisplayName = x.RestoredByUser != null ? x.RestoredByUser.DisplayName : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<BackupEntryDto> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        var userId = currentUserAccessor.UserId ?? throw new InvalidOperationException("Authentication is required.");
        var databasePath = ResolveDatabasePath();
        var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath) ?? SqliteConnectionStringResolver.GetDatabaseBasePath(), "backups");
        Directory.CreateDirectory(backupDirectory);

        var fileName = $"smartstockai-backup-{DateTime.UtcNow:yyyyMMddHHmmss}.db";
        var fullPath = Path.Combine(backupDirectory, fileName);

        File.Copy(databasePath, fullPath, overwrite: true);

        var entry = new BackupEntry
        {
            FileName = fileName,
            FullPath = fullPath,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        dbContext.BackupEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("Backup.Created", nameof(BackupEntry), entry.Id.ToString(), $"Backup '{fileName}' created.", cancellationToken);

        return (await GetAllAsync(cancellationToken)).First(x => x.Id == entry.Id);
    }

    public async Task<BackupEntryDto?> RestoreBackupAsync(int backupEntryId, CancellationToken cancellationToken = default)
    {
        AuthorizationGuard.EnsureAdmin(currentUserAccessor);

        var entry = await dbContext.BackupEntries.FirstOrDefaultAsync(x => x.Id == backupEntryId, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        if (!File.Exists(entry.FullPath))
        {
            throw new FileNotFoundException("Backup file was not found.", entry.FullPath);
        }

        var databasePath = ResolveDatabasePath();
        dbContext.Database.CloseConnection();
        File.Copy(entry.FullPath, databasePath, overwrite: true);

        entry.RestoredAtUtc = DateTime.UtcNow;
        entry.RestoredByUserId = currentUserAccessor.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogWriter.WriteAsync("Backup.Restored", nameof(BackupEntry), entry.Id.ToString(), $"Backup '{entry.FileName}' restored.", cancellationToken);

        return (await GetAllAsync(cancellationToken)).FirstOrDefault(x => x.Id == entry.Id);
    }

    private string ResolveDatabasePath()
    {
        var connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string is not configured.");

        var builder = new SqliteConnectionStringBuilder(connectionString);
        return builder.DataSource;
    }
}
