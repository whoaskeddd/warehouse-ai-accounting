using System;

namespace SmartStockAI.App.Models;

public sealed class AuditLogItem
{
    public int Id { get; init; }
    public DateTime OccurredAt { get; init; }
    public string Actor { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string Severity { get; init; } = "Info";
}
