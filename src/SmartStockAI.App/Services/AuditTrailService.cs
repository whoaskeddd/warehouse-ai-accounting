using System.Collections.ObjectModel;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.Stock;
using SmartStockAI.Core.Enums;

namespace SmartStockAI.App.Services;

public sealed class AuditTrailService
{
    private bool _isSeeded;

    public ObservableCollection<AuditLogItem> Entries { get; } = [];

    public void EnsureSeeded(IEnumerable<StockMovementDto> movements)
    {
        if (_isSeeded)
        {
            return;
        }

        foreach (var movement in movements
                     .OrderByDescending(x => x.CreatedAt)
                     .Take(12)
                     .Reverse())
        {
            Entries.Insert(0, new AuditLogItem
            {
                OccurredAt = movement.CreatedAt.ToLocalTime(),
                Actor = "Система",
                Action = MapMovementAction(movement.Type),
                Target = $"{movement.ProductSku} · {movement.ProductName}",
                Details = string.IsNullOrWhiteSpace(movement.DocumentNumber)
                    ? $"Количество: {movement.Quantity:0.##}, остаток после: {movement.BalanceAfter:0.##}"
                    : $"Документ: {movement.DocumentNumber}, количество: {movement.Quantity:0.##}",
                Severity = "Info"
            });
        }

        _isSeeded = true;
    }

    public void Add(string actor, string action, string target, string details, string severity = "Info")
    {
        Entries.Insert(0, new AuditLogItem
        {
            OccurredAt = DateTime.Now,
            Actor = actor,
            Action = action,
            Target = target,
            Details = details,
            Severity = severity
        });
    }

    private static string MapMovementAction(StockMovementType type) => type switch
    {
        StockMovementType.Receipt => "Проведен приход",
        StockMovementType.Issue => "Проведен расход",
        StockMovementType.Reservation => "Создан резерв",
        StockMovementType.ReservationRelease => "Снят резерв",
        StockMovementType.Adjustment => "Корректировка остатков",
        _ => type.ToString()
    };
}
