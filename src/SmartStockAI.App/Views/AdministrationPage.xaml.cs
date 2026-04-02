using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Contracts.Stock;

namespace SmartStockAI.App.Views;

public partial class AdministrationPage : Page, INotifyPropertyChanged
{
    private readonly BackupWorkspaceService _backupWorkspace;
    private readonly AuditTrailService _auditTrail;
    private readonly AppSessionService _appSession;
    private readonly IStockService _stockService;
    private string _backupComment = "Перед изменениями в справочниках";
    private string _auditFilterText = string.Empty;
    private BackupSnapshotItem? _selectedBackup;

    public AdministrationPage(
        BackupWorkspaceService backupWorkspace,
        AuditTrailService auditTrail,
        AppSessionService appSession,
        IStockService stockService)
    {
        _backupWorkspace = backupWorkspace;
        _auditTrail = auditTrail;
        _appSession = appSession;
        _stockService = stockService;

        InitializeComponent();
        DataContext = this;

        Backups = _backupWorkspace.Snapshots;
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
                RefreshAuditEntries();
            }
        }
    }

    public string BackupTitle => Backups.Count == 0 ? "Резервных копий пока нет" : "Управление резервными копиями";

    public string BackupSummary => $"{Backups.Count} backup-файлов в рабочем списке";

    public string AuditSummary => $"{FilteredEntries.Count} записей";

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        var movements = await _stockService.GetMovementsAsync();
        _auditTrail.EnsureSeeded(movements);
        RefreshAuditEntries();
        OnPropertyChanged(nameof(BackupTitle));
        OnPropertyChanged(nameof(BackupSummary));
    }

    private void CreateBackupButton_OnClick(object sender, RoutedEventArgs e)
    {
        var actor = _appSession.CurrentUser?.DisplayName ?? "Локальный оператор";
        var snapshot = _backupWorkspace.CreateSnapshot(actor, BackupComment);
        SelectedBackup = snapshot;

        _auditTrail.Add(actor, "Создание backup", snapshot.Name, snapshot.Comment);
        RefreshAuditEntries();
        OnPropertyChanged(nameof(BackupTitle));
        OnPropertyChanged(nameof(BackupSummary));
    }

    private void RestoreBackupButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedBackup is null)
        {
            MessageBox.Show("Выбери backup для восстановления.", "Администрирование", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _backupWorkspace.RestoreSnapshot(SelectedBackup);
        var actor = _appSession.CurrentUser?.DisplayName ?? "Локальный оператор";
        _auditTrail.Add(actor, "Восстановление backup", SelectedBackup.Name, "UI-подтверждение восстановления выполнено.", "Warning");

        RefreshAuditEntries();
        OnPropertyChanged(nameof(BackupSummary));
    }

    private void RefreshAuditEntries()
    {
        var filtered = _auditTrail.Entries
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
