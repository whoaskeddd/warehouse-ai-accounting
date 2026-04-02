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
    private string _backupComment = "Manual backup before critical changes";
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

    public string BackupTitle => Backups.Count == 0 ? "No backups yet" : "Backup management";

    public string BackupSummary => $"{Backups.Count} backup files";

    public string AuditSummary => $"{FilteredEntries.Count} records";

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
                    Status = backup.RestoredAtUtc.HasValue ? "Restored" : "Ready"
                });
            }

            _cachedAuditEntries = auditEntries
                .Select(x => new AuditLogItem
                {
                    Id = x.Id,
                    OccurredAt = x.CreatedAtUtc.ToLocalTime(),
                    Actor = x.UserDisplayName ?? "System",
                    Action = x.ActionType,
                    Target = string.IsNullOrWhiteSpace(x.EntityId) ? x.EntityType : $"{x.EntityType} #{x.EntityId}",
                    Details = x.Details,
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
            MessageBox.Show(ex.Message, "Administration", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    "Backup created. Note: the backend contract does not persist custom backup comments yet.",
                    "Administration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Administration", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RestoreBackupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedBackup is null)
        {
            MessageBox.Show("Select a backup first.", "Administration", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(
                $"Restore backup {SelectedBackup.Name}?",
                "Administration",
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
            MessageBox.Show(ex.Message, "Administration", MessageBoxButton.OK, MessageBoxImage.Error);
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
