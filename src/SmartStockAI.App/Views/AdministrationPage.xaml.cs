using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.Audit;
using SmartStockAI.Core.Contracts.Backup;

namespace SmartStockAI.App.Views;

public partial class AdministrationPage : Page, INotifyPropertyChanged
{
    private readonly IBackupService _backupService;
    private readonly IAuditService _auditService;
    private string _backupComment = "Ручная резервная копия перед критическими изменениями";
    private string _auditFilterText = string.Empty;
    private BackupSnapshotItem? _selectedBackup;

    public AdministrationPage(IBackupService backupService, IAuditService auditService)
    {
        _backupService = backupService;
        _auditService = auditService;

        InitializeComponent();
        DataContext = this;

        Backups = [];
        FilteredEntries = [];

        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<BackupSnapshotItem> Backups { get; }

    public ObservableCollection<AuditLogItem> FilteredEntries { get; }

    public BackupSnapshotItem? SelectedBackup
    {
        get => _selectedBackup;
        set => SetField(ref _selectedBackup, value);
    }

    public string BackupComment
    {
        get => _backupComment;
        set => SetField(ref _backupComment, value);
    }

    public string AuditFilterText
    {
        get => _auditFilterText;
        set
        {
            if (SetField(ref _auditFilterText, value))
            {
                RefreshAuditEntries(_cachedAuditEntries);
            }
        }
    }

    public string BackupTitle => Backups.Count == 0 ? "Резервных копий пока нет" : "Управление резервными копиями";

    public string BackupSummary => $"{Backups.Count} файлов резервных копий";

    public string AuditSummary => $"{FilteredEntries.Count} записей";

    private IReadOnlyList<AuditLogItem> _cachedAuditEntries = [];

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            var backups = await _backupService.GetAllAsync();
            var auditEntries = await _auditService.GetAllAsync();

            Backups.Clear();
            foreach (var backup in backups)
            {
                Backups.Add(new BackupSnapshotItem
                {
                    Id = backup.Id,
                    Name = backup.FileName,
                    FullPath = backup.FullPath,
                    CreatedAt = backup.CreatedAtUtc.ToLocalTime(),
                    CreatedBy = backup.CreatedByUserDisplayName,
                    Status = backup.RestoredAtUtc.HasValue ? "Восстановлена" : "Готова"
                });
            }

            _cachedAuditEntries = auditEntries
                .Select(x => new AuditLogItem
                {
                    Id = x.Id,
                    OccurredAt = x.CreatedAtUtc.ToLocalTime(),
                    Actor = x.UserDisplayName ?? "Система",
                    Action = TranslateAction(x.ActionType),
                    Target = TranslateTarget(x.EntityType, x.EntityId),
                    Details = TranslateDetails(x.Details),
                    Severity = GetSeverity(x.ActionType)
                })
                .ToList();

            RefreshAuditEntries(_cachedAuditEntries);
            SelectedBackup = Backups.FirstOrDefault();
            OnPropertyChanged(nameof(BackupTitle));
            OnPropertyChanged(nameof(BackupSummary));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Администрирование", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CreateBackupButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _backupService.CreateBackupAsync();
            await ReloadAsync();

            if (!string.IsNullOrWhiteSpace(BackupComment))
            {
                MessageBox.Show(
                    "Резервная копия создана. Примечание: backend пока не сохраняет пользовательские комментарии к резервным копиям.",
                    "Администрирование",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Администрирование", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RestoreBackupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedBackup is null)
        {
            MessageBox.Show("Сначала выберите резервную копию.", "Администрирование", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(
                $"Восстановить резервную копию {SelectedBackup.Name}?",
                "Администрирование",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _backupService.RestoreBackupAsync(SelectedBackup.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Администрирование", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshAuditEntries(IReadOnlyList<AuditLogItem> source)
    {
        var filtered = source
            .Where(x => string.IsNullOrWhiteSpace(AuditFilterText)
                || x.Actor.Contains(AuditFilterText, StringComparison.OrdinalIgnoreCase)
                || x.Action.Contains(AuditFilterText, StringComparison.OrdinalIgnoreCase)
                || x.Target.Contains(AuditFilterText, StringComparison.OrdinalIgnoreCase)
                || x.Details.Contains(AuditFilterText, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.OccurredAt)
            .ToList();

        FilteredEntries.Clear();
        foreach (var item in filtered)
        {
            FilteredEntries.Add(item);
        }

        OnPropertyChanged(nameof(AuditSummary));
    }

    private static string GetSeverity(string actionType)
    {
        if (actionType.Contains("Deleted", StringComparison.OrdinalIgnoreCase)
            || actionType.Contains("Restored", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        return "Info";
    }

    private static string TranslateAction(string actionType) => actionType switch
    {
        "Auth.Login" => "Вход в систему",
        "Auth.Logout" => "Выход из системы",
        "User.Created" => "Пользователь создан",
        "User.Updated" => "Пользователь обновлён",
        "User.Deleted" => "Пользователь удалён",
        "Backup.Created" => "Резервная копия создана",
        "Backup.Restored" => "Резервная копия восстановлена",
        _ => actionType
    };

    private static string TranslateTarget(string entityType, string? entityId)
    {
        var entityName = entityType switch
        {
            "User" => "Пользователь",
            "BackupEntry" => "Резервная копия",
            "Product" => "Товар",
            "InventorySession" => "Инвентаризация",
            "StockDocument" => "Складской документ",
            _ => entityType
        };

        return string.IsNullOrWhiteSpace(entityId) ? entityName : $"{entityName} #{entityId}";
    }

    private static string TranslateDetails(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return string.Empty;
        }

        return details
            .Replace("User logged out.", "Пользователь вышел из системы.", StringComparison.Ordinal)
            .Replace("Restored backup", "Восстановлена резервная копия", StringComparison.Ordinal)
            .Replace("created with role", "создан с ролью", StringComparison.Ordinal)
            .Replace("updated.", "обновлён.", StringComparison.Ordinal)
            .Replace("deleted.", "удалён.", StringComparison.Ordinal)
            .Replace("logged in.", "вошёл в систему.", StringComparison.Ordinal);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
