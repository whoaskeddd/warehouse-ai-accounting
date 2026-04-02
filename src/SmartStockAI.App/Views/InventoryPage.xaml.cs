using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Contracts.Products;

namespace SmartStockAI.App.Views;

public partial class InventoryPage : Page, INotifyPropertyChanged
{
    private readonly IProductService _productService;
    private readonly AppSessionService _appSession;
    private readonly AuditTrailService _auditTrail;
    private readonly ObservableCollection<InventoryCountItem> _allItems = [];
    private InventoryCountItem? _selectedInventoryItem;
    private LookupItem? _selectedDifferenceFilter;
    private string _searchText = string.Empty;
    private string _countedStockText = string.Empty;
    private string _countCommentText = string.Empty;

    public InventoryPage(IProductService productService, AppSessionService appSession, AuditTrailService auditTrail)
    {
        _productService = productService;
        _appSession = appSession;
        _auditTrail = auditTrail;

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

    public string SessionSummary => $"{FilteredItems.Count} позиций в таблице, {DiscrepanciesCount} с расхождениями";

    public string SelectedItemTitle => SelectedInventoryItem is null
        ? "Выбери позицию из списка"
        : $"{SelectedInventoryItem.Sku} · {SelectedInventoryItem.ProductName}";

    public string DiscrepancyTitle => DiscrepanciesCount == 0 ? "Расхождений нет" : $"Акт на {DiscrepanciesCount} позиций";

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await LoadItemsAsync();
    }

    private async Task LoadItemsAsync()
    {
        var products = await _productService.GetAllAsync();

        _allItems.Clear();
        foreach (var product in products.OrderBy(x => x.Name))
        {
            _allItems.Add(new InventoryCountItem
            {
                ProductId = product.Id,
                Sku = product.Sku,
                ProductName = product.Name,
                CategoryName = product.CategoryName ?? "Без категории",
                LocationName = product.LocationName ?? "Без локации",
                Unit = product.Unit,
                ExpectedStock = product.CurrentStock,
                CountedStock = product.CurrentStock
            });
        }

        SelectedInventoryItem = _allItems.FirstOrDefault();
        RefreshFilteredItems();
        RefreshDiscrepancies();
    }

    private void ApplyCountButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedInventoryItem is null)
        {
            MessageBox.Show("Выбери позицию для ввода фактического остатка.", "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(CountedStockText, out var countedStock))
        {
            MessageBox.Show("Фактический остаток должен быть числом.", "Инвентаризация", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedInventoryItem.CountedStock = countedStock;
        SelectedInventoryItem.Comment = CountCommentText.Trim();

        RefreshFilteredItems();
        RefreshDiscrepancies();

        var actor = _appSession.CurrentUser?.DisplayName ?? "Локальный оператор";
        _auditTrail.Add(
            actor,
            "Фиксация пересчета",
            SelectedInventoryItem.Sku,
            $"Ожидалось {SelectedInventoryItem.ExpectedStock:0.##}, факт {SelectedInventoryItem.CountedStock:0.##}, разница {SelectedInventoryItem.Difference:0.##}.");
    }

    private void ResetSessionButton_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _allItems)
        {
            item.CountedStock = item.ExpectedStock;
            item.Comment = string.Empty;
        }

        if (SelectedInventoryItem is not null)
        {
            CountedStockText = SelectedInventoryItem.ExpectedStock.ToString(CultureInfo.InvariantCulture);
            CountCommentText = string.Empty;
        }

        RefreshFilteredItems();
        RefreshDiscrepancies();

        var actor = _appSession.CurrentUser?.DisplayName ?? "Локальный оператор";
        _auditTrail.Add(actor, "Сброс инвентаризации", "Текущая сессия", "Все фактические остатки возвращены к ожидаемым значениям.", "Warning");
    }

    private void GenerateActButton_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshDiscrepancies();

        var actor = _appSession.CurrentUser?.DisplayName ?? "Локальный оператор";
        _auditTrail.Add(
            actor,
            "Формирование акта расхождений",
            "Инвентаризация",
            DiscrepanciesCount == 0
                ? "Акт сформирован без расхождений."
                : $"В акт попало {DiscrepanciesCount} позиций.");

        MessageBox.Show(
            DiscrepanciesCount == 0
                ? "Расхождений не найдено. Акт пустой."
                : $"Акт расхождений сформирован: {DiscrepanciesCount} позиций.",
            "Инвентаризация",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
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
    }

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
