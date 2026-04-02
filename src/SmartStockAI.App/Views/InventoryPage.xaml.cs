using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Contracts.Inventory;
using SmartStockAI.Core.Contracts.Products;

namespace SmartStockAI.App.Views;

public partial class InventoryPage : Page, INotifyPropertyChanged
{
    private readonly IInventoryService _inventoryService;
    private readonly IProductService _productService;
    private readonly AppSessionService _appSession;
    private readonly ObservableCollection<InventoryCountItem> _allItems = [];
    private InventorySessionDto? _currentSession;
    private InventoryCountItem? _selectedInventoryItem;
    private LookupItem? _selectedDifferenceFilter;
    private string _searchText = string.Empty;
    private string _countedStockText = string.Empty;
    private string _countCommentText = string.Empty;

    public InventoryPage(IInventoryService inventoryService, IProductService productService, AppSessionService appSession)
    {
        _inventoryService = inventoryService;
        _productService = productService;
        _appSession = appSession;

        InitializeComponent();
        DataContext = this;

        FilteredItems = [];
        Discrepancies = [];
        DifferenceFilterOptions =
        [
            new LookupItem { Id = 0, Name = "Все позиции" },
            new LookupItem { Id = 1, Name = "Только расхождения" }
        ];
        SelectedDifferenceFilter = DifferenceFilterOptions[0];

        _appSession.CurrentUserChanged += AppSession_OnCurrentUserChanged;
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<InventoryCountItem> FilteredItems { get; }

    public ObservableCollection<InventoryCountItem> Discrepancies { get; }

    public List<LookupItem> DifferenceFilterOptions { get; }

    public InventoryCountItem? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set
        {
            if (SetField(ref _selectedInventoryItem, value))
            {
                CountedStockText = value?.CountedStock.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                CountCommentText = value?.Comment ?? string.Empty;
                OnPropertyChanged(nameof(SelectedItemTitle));
            }
        }
    }

    public LookupItem? SelectedDifferenceFilter
    {
        get => _selectedDifferenceFilter;
        set
        {
            if (SetField(ref _selectedDifferenceFilter, value))
            {
                RefreshFilteredItems();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                RefreshFilteredItems();
            }
        }
    }

    public string CountedStockText
    {
        get => _countedStockText;
        set => SetField(ref _countedStockText, value);
    }

    public string CountCommentText
    {
        get => _countCommentText;
        set => SetField(ref _countCommentText, value);
    }

    public int TotalItemsCount => _allItems.Count;

    public int DiscrepanciesCount => _allItems.Count(x => x.HasDifference);

    public string CurrentOperator => _appSession.CurrentUser?.DisplayName ?? "Не выбран";

    public string SessionSummary
    {
        get
        {
            var sessionNumber = _currentSession?.Number ?? "-";
            return $"{FilteredItems.Count} позиций в сессии {sessionNumber}, расхождений: {DiscrepanciesCount}";
        }
    }

    public string SelectedItemTitle => SelectedInventoryItem is null
        ? "Выберите позицию из списка"
        : $"{SelectedInventoryItem.Sku} · {SelectedInventoryItem.ProductName}";

    public string DiscrepancyTitle => DiscrepanciesCount == 0 ? "Расхождений нет" : $"Акт расхождений: {DiscrepanciesCount} позиций";

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await ReloadAsync(createNewDraft: false);
    }

    private async Task ReloadAsync(bool createNewDraft)
    {
        try
        {
            var sessions = await _inventoryService.GetAllAsync();
            _currentSession = createNewDraft
                ? null
                : sessions.FirstOrDefault(x => x.Status == Core.Enums.InventorySessionStatus.Draft);

            if (_currentSession is null)
            {
                _currentSession = await _inventoryService.CreateAsync(new CreateInventorySessionRequest
                {
                    Number = GenerateSessionNumber(),
                    Comment = $"Создано пользователем {_appSession.CurrentUser?.DisplayName ?? "приложение"}"
                });
            }

            await LoadItemsAsync(_currentSession);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadItemsAsync(InventorySessionDto session)
    {
        var products = await _productService.GetAllAsync();
        var countedLines = session.Lines.ToDictionary(x => x.ProductId);

        _allItems.Clear();
        foreach (var product in products.OrderBy(x => x.Name))
        {
            countedLines.TryGetValue(product.Id, out var line);

            _allItems.Add(new InventoryCountItem
            {
                ProductId = product.Id,
                Sku = product.Sku,
                ProductName = product.Name,
                CategoryName = product.CategoryName ?? "Без категории",
                LocationName = product.LocationName ?? "Без локации",
                Unit = product.Unit,
                ExpectedStock = line?.ExpectedStock ?? product.CurrentStock,
                CountedStock = line?.ActualStock ?? product.CurrentStock,
                Comment = line?.Comment ?? string.Empty
            });
        }

        SelectedInventoryItem = _allItems.FirstOrDefault();
        RefreshFilteredItems();
        RefreshDiscrepancies();
    }

    private async void ApplyCountButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentSession is null || SelectedInventoryItem is null)
        {
            MessageBox.Show("Сначала выберите позицию.", "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(CountedStockText, out var countedStock))
        {
            MessageBox.Show("Фактический остаток должен быть числом.", "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var session = await _inventoryService.SaveCountAsync(_currentSession.Id, new SaveInventoryCountRequest
            {
                ProductId = SelectedInventoryItem.ProductId,
                ActualStock = countedStock,
                Comment = string.IsNullOrWhiteSpace(CountCommentText) ? null : CountCommentText.Trim()
            });

            if (session is null)
            {
                MessageBox.Show("Сессия инвентаризации не найдена.", "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentSession = session;
            await LoadItemsAsync(session);
            SelectedInventoryItem = _allItems.FirstOrDefault(x => x.ProductId == session.Lines.LastOrDefault()?.ProductId) ?? _allItems.FirstOrDefault();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ResetSessionButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Начать новую сессию инвентаризации? Текущие черновые значения останутся в истории.",
                "Инвентаризация",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        await ReloadAsync(createNewDraft: true);
    }

    private async void GenerateActButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_currentSession is null)
        {
            return;
        }

        try
        {
            var completed = await _inventoryService.CompleteAsync(_currentSession.Id);
            if (completed is null)
            {
                MessageBox.Show("Сессия инвентаризации не найдена.", "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var report = completed.DiscrepancyReport;
            MessageBox.Show(
                report is null
                    ? $"Сессия {completed.Number} завершена без расхождений."
                    : $"Сессия {completed.Number} завершена. Отчёт {report.Number}: {report.TotalItems} позиций, суммарное расхождение {report.TotalVariance:0.##}.",
                "Инвентаризация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await ReloadAsync(createNewDraft: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshFilteredItems()
    {
        var filtered = _allItems
            .Where(x => string.IsNullOrWhiteSpace(SearchText)
                || x.Sku.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || x.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || x.CategoryName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || x.LocationName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        if (SelectedDifferenceFilter?.Id == 1)
        {
            filtered = filtered.Where(x => x.HasDifference);
        }

        FilteredItems.Clear();
        foreach (var item in filtered.OrderBy(x => x.ProductName))
        {
            FilteredItems.Add(item);
        }

        OnPropertyChanged(nameof(TotalItemsCount));
        OnPropertyChanged(nameof(DiscrepanciesCount));
        OnPropertyChanged(nameof(SessionSummary));
        OnPropertyChanged(nameof(DiscrepancyTitle));
    }

    private void RefreshDiscrepancies()
    {
        Discrepancies.Clear();
        foreach (var item in _allItems.Where(x => x.HasDifference).OrderByDescending(x => Math.Abs(x.Difference)))
        {
            Discrepancies.Add(item);
        }

        OnPropertyChanged(nameof(DiscrepanciesCount));
        OnPropertyChanged(nameof(SessionSummary));
        OnPropertyChanged(nameof(DiscrepancyTitle));
    }

    private void AppSession_OnCurrentUserChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentOperator));
        OnPropertyChanged(nameof(SessionSummary));
    }

    private static string GenerateSessionNumber() => $"INV-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out value);
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
