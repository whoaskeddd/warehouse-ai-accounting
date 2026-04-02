using System;

namespace SmartStockAI.App.Models;

public sealed class BackupSnapshotItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
