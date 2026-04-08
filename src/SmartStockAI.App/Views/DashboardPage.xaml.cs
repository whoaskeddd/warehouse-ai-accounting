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
using SmartStockAI.Core.Contracts.AI;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Stock;
using SmartStockAI.Core.Enums;

namespace SmartStockAI.App.Views;

public partial class DashboardPage : Page, INotifyPropertyChanged
{
    private readonly IAiService _aiService;
    private readonly IProductService _productService;
    private readonly IStockService _stockService;
    private int _criticalStockCount;
    private int _todayMovementsCount;
    private int _draftDocumentsCount;
    private string _forecastStatus = "Прогноз еще не рассчитан";
    private string _forecastChartTitle = "AI-прогноз спроса";
    private string _forecastChartSummary = "Нет данных для визуализации прогноза.";
    private string _focusTitle = "Склад стабилен";
    private string _focusDescription = "Критичных сигналов пока нет. Можно продолжать работу с документами и номенклатурой.";
    private bool _isLoaded;

    public DashboardPage(IAiService aiService, IProductService productService, IStockService stockService)
    {
        _aiService = aiService;
        _productService = productService;
        _stockService = stockService;

        InitializeComponent();
        DataContext = this;

        RecentMovements = [];
        CriticalItems = [];
        PurchaseRecommendations = [];
        ForecastChartItems = [];
        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MovementHistoryItem> RecentMovements { get; }
    public ObservableCollection<AiCriticalStockItemDto> CriticalItems { get; }
    public ObservableCollection<AiPurchaseRecommendationDto> PurchaseRecommendations { get; }
    public ObservableCollection<DashboardForecastBarItem> ForecastChartItems { get; }

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

    public string ForecastStatus
    {
        get => _forecastStatus;
        set => SetField(ref _forecastStatus, value);
    }

    public string ForecastChartTitle
    {
        get => _forecastChartTitle;
        set => SetField(ref _forecastChartTitle, value);
    }

    public string ForecastChartSummary
    {
        get => _forecastChartSummary;
        set => SetField(ref _forecastChartSummary, value);
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

    private async void RecalculateForecastButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _aiService.RefreshModelsAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Дашборд", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        var aiDashboard = await _aiService.GetDashboardAsync();

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

        CriticalItems.Clear();
        foreach (var item in aiDashboard.CriticalItems)
        {
            CriticalItems.Add(item);
        }

        PurchaseRecommendations.Clear();
        foreach (var item in aiDashboard.PurchaseRecommendations)
        {
            PurchaseRecommendations.Add(item);
        }

        ForecastStatus = aiDashboard.LastForecastCalculatedAtUtc.HasValue
            ? $"Последний расчет: {aiDashboard.LastForecastCalculatedAtUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}"
            : "Прогноз еще не рассчитан";

        await UpdateForecastChartAsync(aiDashboard);
        UpdateFocus(products.Count, movements.Count, aiDashboard);
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

    private async Task UpdateForecastChartAsync(AiDashboardDto aiDashboard)
    {
        ForecastChartItems.Clear();

        var topRecommendation = aiDashboard.PurchaseRecommendations.FirstOrDefault();
        if (topRecommendation is null)
        {
            ForecastChartTitle = "AI-прогноз спроса";
            ForecastChartSummary = "Нет позиции с активной закупочной рекомендацией.";
            return;
        }

        var analytics = await _aiService.GetProductAnalyticsAsync(topRecommendation.ProductId);
        if (analytics is null)
        {
            ForecastChartTitle = "AI-прогноз спроса";
            ForecastChartSummary = "Не удалось загрузить данные прогноза.";
            return;
        }

        ForecastChartTitle = $"AI-прогноз: {analytics.ProductName}";
        ForecastChartSummary = $"Спрос/мес {analytics.AverageMonthlyDemand:0.###} · Заказать {analytics.RecommendedOrder:0.###}";

        var points = analytics.History
            .TakeLast(4)
            .Concat(analytics.Forecast.Take(4))
            .ToList();

        if (points.Count == 0)
        {
            return;
        }

        var maxValue = points.Max(x => x.Quantity);
        var scale = maxValue <= 0 ? 1d : 112d / (double)maxValue;

        foreach (var point in points)
        {
            ForecastChartItems.Add(new DashboardForecastBarItem
            {
                Label = point.Period.ToString("MM.yy"),
                Quantity = point.Quantity,
                Height = Math.Max(8d, Math.Round((double)point.Quantity * scale, 2)),
                IsForecast = point.IsForecast
            });
        }
    }

    private void UpdateFocus(int productsCount, int movementsCount, AiDashboardDto aiDashboard)
    {
        if (aiDashboard.PurchaseRecommendations.Count > 0)
        {
            var topRecommendation = aiDashboard.PurchaseRecommendations[0];
            FocusTitle = "Нужен заказ по критичным позициям";
            FocusDescription = $"Лидирует {topRecommendation.ProductName}: рекомендованный заказ {topRecommendation.RecommendedOrder:0.###}. Проверь дашборд дефицита и создай приход.";
            return;
        }

        if (CriticalStockCount > 0)
        {
            FocusTitle = "Проверь критичные остатки";
            FocusDescription = $"Сейчас {CriticalStockCount} позиций требуют внимания. Начни с товаров и затем оформи приход.";
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
