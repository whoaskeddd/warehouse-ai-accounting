using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SmartStockAI.Core.Contracts.Reports;

namespace SmartStockAI.App.Views;

public partial class ReportsPage : Page, INotifyPropertyChanged
{
    private readonly IReportService _reportService;
    private bool _isLoaded;
    private ReportDefinitionDto? _selectedDefinition;
    private DataView? _previewRows;
    private string _previewTitle = "Отчет не выбран";
    private string _previewSummary = "Выбери стандартный отчет, чтобы увидеть данные.";
    private string _previewMetaText = "0 строк";

    public ReportsPage(IReportService reportService)
    {
        _reportService = reportService;
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ReportDefinitionDto> ReportDefinitions { get; } = [];
    public ReportDefinitionDto? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (SetField(ref _selectedDefinition, value) && _isLoaded && value is not null)
            {
                _ = LoadLivePreviewAsync(value.Key);
            }
        }
    }

    public DataView? PreviewRows
    {
        get => _previewRows;
        private set => SetField(ref _previewRows, value);
    }

    public string PreviewTitle
    {
        get => _previewTitle;
        private set => SetField(ref _previewTitle, value);
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => SetField(ref _previewSummary, value);
    }

    public string PreviewMetaText
    {
        get => _previewMetaText;
        private set => SetField(ref _previewMetaText, value);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var definitions = await _reportService.GetDefinitionsAsync();
        ReportDefinitions.Clear();
        foreach (var item in definitions)
        {
            ReportDefinitions.Add(item);
        }

        _isLoaded = true;
        SelectedDefinition = ReportDefinitions.FirstOrDefault();
    }

    private async void RefreshPreviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedDefinition is null)
        {
            return;
        }

        await LoadLivePreviewAsync(SelectedDefinition.Key);
    }

    private async void ExportExcelButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedDefinition is null)
        {
            MessageBox.Show("Сначала выбери отчет.", "Отчеты", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"{SelectedDefinition.Key}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var content = await _reportService.ExportReportToExcelAsync(SelectedDefinition.Key);
            await File.WriteAllBytesAsync(dialog.FileName, content);
            MessageBox.Show("Отчет выгружен в Excel.", "Отчеты", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Отчеты", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadLivePreviewAsync(string reportKey)
    {
        try
        {
            var report = await _reportService.BuildReportAsync(reportKey);
            ApplyPreview(report, "Предпросмотр");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Отчеты", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyPreview(ReportResultDto report, string sourceLabel)
    {
        PreviewTitle = $"{report.ReportName} · {sourceLabel}";
        PreviewSummary = report.Summary;
        PreviewMetaText = $"{report.Rows.Count} строк · {report.GeneratedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}";
        PreviewRows = BuildDataTable(report).DefaultView;
    }

    private static DataTable BuildDataTable(ReportResultDto report)
    {
        var table = new DataTable(report.ReportName);
        foreach (var column in report.Columns)
        {
            table.Columns.Add(column.Title);
        }

        foreach (var row in report.Rows)
        {
            var values = report.Columns
                .Select(column => row.TryGetValue(column.Key, out var value) ? value : string.Empty)
                .Cast<object>()
                .ToArray();

            table.Rows.Add(values);
        }

        return table;
    }

    private void PreviewDataGrid_OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is not DataGridTextColumn textColumn)
        {
            return;
        }

        var textStyle = new Style(typeof(TextBlock), (Style)FindResource("CenteredDataGridTextStyle"));
        textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        textStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        textStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
        textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));

        textColumn.ElementStyle = textStyle;
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
