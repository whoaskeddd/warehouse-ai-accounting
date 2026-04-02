using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Stock;
using SmartStockAI.Core.Enums;

namespace SmartStockAI.App.Views;

public partial class DashboardPage : Page, INotifyPropertyChanged
{
    private readonly IProductService _productService;
    private readonly IStockService _stockService;
    private int _criticalStockCount;
    private int _todayMovementsCount;
    private int _draftDocumentsCount;
    private string _focusTitle = "Склад стабилен";
    private string _focusDescription = "Критичных сигналов пока нет. Можно продолжать работу с документами и номенклатурой.";
    private bool _isLoaded;

    public DashboardPage(IProductService productService, IStockService stockService)
    {
        _productService = productService;
        _stockService = stockService;

        InitializeComponent();
        DataContext = this;

        RecentMovements = [];
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MovementHistoryItem> RecentMovements { get; }

    public int CriticalStockCount
    {
        get => _criticalStockCount;
        set => SetField(ref _criticalStockCount, value);
    }

    public int TodayMovementsCount
    {
        get => _todayMovementsCount;
        set => SetField(ref _todayMovementsCount, value);
    }

    public int DraftDocumentsCount
    {
        get => _draftDocumentsCount;
        set => SetField(ref _draftDocumentsCount, value);
    }

    public string FocusTitle
    {
        get => _focusTitle;
        set => SetField(ref _focusTitle, value);
    }

    public string FocusDescription
    {
        get => _focusDescription;
        set => SetField(ref _focusDescription, value);
    }

    public string RecentMovementsSummary => $"{RecentMovements.Count} последних операций";

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        await RefreshWithHandlingAsync();
    }

    private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshWithHandlingAsync();
    }

    private void OpenProductsButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigateTo("Products");
    }

    private void OpenInboundButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigateTo("Inbound");
    }

    private void OpenOutboundButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigateTo("Outbound");
    }

    private async Task RefreshAsync()
    {
        var products = await _productService.GetAllAsync();
        var documents = await _stockService.GetDocumentsAsync();
        var movements = await _stockService.GetMovementsAsync();

        CriticalStockCount = products.Count(x => x.AvailableStock <= x.MinStock);
        DraftDocumentsCount = documents.Count(x => x.Status == StockDocumentStatus.Draft);

        var today = DateTime.Now.Date;
        TodayMovementsCount = movements.Count(x => x.CreatedAt.ToLocalTime().Date == today);

        var recent = movements
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(MapMovementHistoryItem)
            .ToList();

        RecentMovements.Clear();
        foreach (var item in recent)
        {
            RecentMovements.Add(item);
        }

        UpdateFocus(products.Count, movements.Count);
        OnPropertyChanged(nameof(RecentMovementsSummary));
    }

    private async Task RefreshWithHandlingAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Дашборд", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateFocus(int productsCount, int movementsCount)
    {
        if (CriticalStockCount > 0)
        {
            FocusTitle = "Проверь критичные остатки";
            FocusDescription = $"Сейчас {CriticalStockCount} поз. требуют внимания. Начни с товаров и затем оформи приход.";
            return;
        }

        if (DraftDocumentsCount > 0)
        {
            FocusTitle = "Есть незавершенные документы";
            FocusDescription = $"Открыто {DraftDocumentsCount} черновик(ов). Проверь приход и расход перед следующими операциями.";
            return;
        }

        FocusTitle = movementsCount == 0 ? "Заполни склад данными" : "Склад стабилен";
        FocusDescription = movementsCount == 0
            ? $"Пока нет движений. Добавь товары в каталог и создай первый документ. Всего товаров в базе: {productsCount}."
            : "Критичных сигналов пока нет. Можно продолжать работу с документами и номенклатурой.";
    }

    private static MovementHistoryItem MapMovementHistoryItem(StockMovementDto movement)
    {
        return new MovementHistoryItem
        {
            OccurredAt = movement.CreatedAt.ToLocalTime(),
            ProductName = movement.ProductName,
            Sku = movement.ProductSku,
            MovementType = MapMovementType(movement.Type),
            DocumentNumber = movement.DocumentNumber ?? "Без документа",
            Quantity = movement.Quantity,
            BalanceAfter = movement.BalanceAfter,
            Comment = movement.Comment ?? string.Empty
        };
    }

    private static string MapMovementType(StockMovementType type) => type switch
    {
        StockMovementType.Receipt => "Приход",
        StockMovementType.Issue => "Расход",
        StockMovementType.Reservation => "Резерв",
        StockMovementType.ReservationRelease => "Снятие резерва",
        StockMovementType.Adjustment => "Корректировка",
        _ => type.ToString()
    };

    private void NavigateTo(string key)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateTo(key);
        }
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
