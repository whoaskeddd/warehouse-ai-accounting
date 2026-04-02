using System.Collections.ObjectModel;
using SmartStockAI.App.Models;

namespace SmartStockAI.App.Services;

public sealed class BackupWorkspaceService
{
    public ObservableCollection<BackupSnapshotItem> Snapshots { get; } = [];

    public BackupSnapshotItem CreateSnapshot(string actor, string comment)
    {
        var snapshot = new BackupSnapshotItem
        {
            Id = 0,
            Name = $"backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            FullPath = string.IsNullOrWhiteSpace(comment) ? string.Empty : comment.Trim(),
            CreatedAt = DateTime.Now,
            CreatedBy = actor,
            Status = "Ready"
        };

        Snapshots.Insert(0, snapshot);
        TrimToRecent();
        return snapshot;
    }

    public BackupSnapshotItem? RestoreSnapshot(BackupSnapshotItem? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        snapshot.Status = "Restored";
        return snapshot;
    }

    private void TrimToRecent()
    {
        while (Snapshots.Count > 7)
        {
            Snapshots.RemoveAt(Snapshots.Count - 1);
        }
    }
}
