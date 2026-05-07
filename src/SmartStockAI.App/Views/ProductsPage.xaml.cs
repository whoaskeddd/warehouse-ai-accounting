using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.AI;
using SmartStockAI.Core.Contracts.Categories;
using SmartStockAI.Core.Contracts.Locations;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Suppliers;

namespace SmartStockAI.App.Views;

public partial class ProductsPage : Page
{
    private readonly IAiService _aiService;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ISupplierService _supplierService;
    private readonly ILocationService _locationService;
    private readonly ObservableCollection<ProductListItem> _products = [];
    private readonly ObservableCollection<AiForecastPointDto> _forecastPoints = [];
    private List<LookupItem> _categories = [];
    private List<LookupItem> _suppliers = [];
    private List<LookupItem> _locations = [];
    private int? _selectedProductId;
    private bool _isApplyingSuggestedCategory;

    public ProductsPage(
        IAiService aiService,
        IProductService productService,
        ICategoryService categoryService,
        ISupplierService supplierService,
        ILocationService locationService)
    {
        _aiService = aiService;
        _productService = productService;
        _categoryService = categoryService;
        _supplierService = supplierService;
        _locationService = locationService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ProductsDataGrid.ItemsSource is null)
        {
            ProductsDataGrid.ItemsSource = _products;
        }

        if (ForecastDataGrid.ItemsSource is null)
        {
            ForecastDataGrid.ItemsSource = _forecastPoints;
        }

        await LoadLookupsAsync();
        await LoadProductsAsync();
        ResetEditor();
    }

    private async Task LoadLookupsAsync()
    {
        _categories = (await _categoryService.GetAllAsync())
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        _suppliers = (await _supplierService.GetAllAsync())
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        _locations = (await _locationService.GetAllAsync())
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        var categoryOptions = CreateOptions("Все категории", _categories);
        var supplierOptions = CreateOptions("Все поставщики", _suppliers);
        var categoryEditorOptions = CreateOptions("Без категории", _categories);
        var supplierEditorOptions = CreateOptions("Без поставщика", _suppliers);
        var locationEditorOptions = CreateOptions("Без локации", _locations);

        CategoryFilterComboBox.ItemsSource = categoryOptions;
        SupplierFilterComboBox.ItemsSource = supplierOptions;
        CategoryEditorComboBox.ItemsSource = categoryEditorOptions;
        SupplierEditorComboBox.ItemsSource = supplierEditorOptions;
        LocationEditorComboBox.ItemsSource = locationEditorOptions;

        CategoryFilterComboBox.SelectedIndex = 0;
        SupplierFilterComboBox.SelectedIndex = 0;
        CategoryEditorComboBox.SelectedIndex = 0;
        SupplierEditorComboBox.SelectedIndex = 0;
        LocationEditorComboBox.SelectedIndex = 0;
    }

    private static List<LookupItem> CreateOptions(string emptyTitle, IEnumerable<LookupItem> items)
    {
        return [new LookupItem { Id = null, Name = emptyTitle }, .. items];
    }

    private async Task LoadProductsAsync()
    {
        var query = new SearchProductsRequest
        {
            SearchText = SearchBox.Text.Trim(),
            CategoryId = CategoryFilterComboBox.SelectedValue as int?,
            SupplierId = SupplierFilterComboBox.SelectedValue as int?,
            OnlyCritical = CriticalOnlyCheckBox.IsChecked == true
        };

        var items = (await _productService.SearchAsync(query))
            .Select(x => new ProductListItem
            {
                Id = x.Id,
                Sku = x.Sku,
                Name = x.Name,
                CategoryName = x.CategoryName ?? "Без категории",
                SupplierName = x.SupplierName ?? "Без поставщика",
                LocationName = x.LocationName ?? "Без локации",
                CurrentStock = x.CurrentStock,
                MinStock = x.MinStock,
                PurchasePrice = x.PurchasePrice,
                SalePrice = x.SalePrice
            })
            .ToList();

        _products.Clear();
        foreach (var item in items)
        {
            _products.Add(item);
        }

        ProductsCountText.Text = $"{_products.Count} позиций";
    }

    private async void ProductsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductsDataGrid.SelectedItem is not ProductListItem item)
        {
            return;
        }

        var product = await _productService.GetByIdAsync(item.Id);
        if (product is null)
        {
            return;
        }

        _selectedProductId = product.Id;
        EditorTitleText.Text = $"Товар · {product.Sku}";
        SkuTextBox.Text = product.Sku;
        NameTextBox.Text = product.Name;
        UnitTextBox.Text = product.Unit;
        CurrentStockTextBox.Text = product.CurrentStock.ToString(CultureInfo.InvariantCulture);
        MinStockTextBox.Text = product.MinStock.ToString(CultureInfo.InvariantCulture);
        PurchasePriceTextBox.Text = product.PurchasePrice.ToString(CultureInfo.InvariantCulture);
        SalePriceTextBox.Text = product.SalePrice.ToString(CultureInfo.InvariantCulture);
        CategoryEditorComboBox.SelectedValue = product.CategoryId;
        SupplierEditorComboBox.SelectedValue = product.SupplierId;
        LocationEditorComboBox.SelectedValue = product.LocationId;
        await LoadAnalyticsAsync(product.Id, product.Name);
    }

    private async Task LoadAnalyticsAsync(int productId, string productName)
    {
        var analytics = await _aiService.GetProductAnalyticsAsync(productId);
        var suggestion = await _aiService.SuggestCategoryAsync(productName);

        SuggestionTextBlock.Text = suggestion is null
            ? "AI-рекомендация по категории недоступна."
            : suggestion.IsStrongRecommendation
                ? $"AI рекомендует категорию: {suggestion.CategoryName} ({suggestion.Confidence:P0})."
                : $"Слабая рекомендация: {suggestion.CategoryName} ({suggestion.Confidence:P0}).";

        ApplySuggestedCategoryButton.IsEnabled = suggestion is not null;
        ApplySuggestedCategoryButton.Tag = suggestion;
        IgnoreSuggestedCategoryButton.IsEnabled = suggestion is not null;

        AnalyticsSummaryText.Text = analytics is null || analytics.Forecast.Count == 0
            ? "Прогноз пока не рассчитан."
            : $"Спрос/мес: {analytics.AverageMonthlyDemand:0.###} · Заказать: {analytics.RecommendedOrder:0.###} · Дефицит: {analytics.ProjectedDeficit:0.###}";

        AnalyticsMetaText.Text = analytics?.ForecastCalculatedAtUtc is null
            ? "Нет даты последнего расчета."
            : $"Последний расчет: {analytics.ForecastCalculatedAtUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}";

        _forecastPoints.Clear();
        if (analytics is not null)
        {
            foreach (var point in analytics.History)
            {
                _forecastPoints.Add(point);
            }

            foreach (var point in analytics.Forecast.Take(12))
            {
                _forecastPoints.Add(point);
            }
        }
    }

    private async void SaveProductButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SkuTextBox.Text) || string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("У товара должны быть заполнены артикул и наименование.", "Товары", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(CurrentStockTextBox.Text, out var currentStock)
            || !TryParseDecimal(MinStockTextBox.Text, out var minStock)
            || !TryParseDecimal(PurchasePriceTextBox.Text, out var purchasePrice)
            || !TryParseDecimal(SalePriceTextBox.Text, out var salePrice))
        {
            MessageBox.Show("Проверь числовые поля: остаток, min остаток и цены.", "Товары", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var baseRequest = new CreateProductRequest
        {
            Sku = SkuTextBox.Text.Trim(),
            Name = NameTextBox.Text.Trim(),
            Unit = string.IsNullOrWhiteSpace(UnitTextBox.Text) ? "шт" : UnitTextBox.Text.Trim(),
            CurrentStock = currentStock,
            MinStock = minStock,
            PurchasePrice = purchasePrice,
            SalePrice = salePrice,
            CategoryId = CategoryEditorComboBox.SelectedValue as int?,
            SupplierId = SupplierEditorComboBox.SelectedValue as int?,
            LocationId = LocationEditorComboBox.SelectedValue as int?
        };

        try
        {
            ProductDto? product;
            if (_selectedProductId.HasValue)
            {
                product = await _productService.UpdateAsync(_selectedProductId.Value, new UpdateProductRequest
                {
                    Sku = baseRequest.Sku,
                    Name = baseRequest.Name,
                    Unit = baseRequest.Unit,
                    CurrentStock = baseRequest.CurrentStock,
                    MinStock = baseRequest.MinStock,
                    PurchasePrice = baseRequest.PurchasePrice,
                    SalePrice = baseRequest.SalePrice,
                    CategoryId = baseRequest.CategoryId,
                    SupplierId = baseRequest.SupplierId,
                    LocationId = baseRequest.LocationId
                });
            }
            else
            {
                product = await _productService.CreateAsync(baseRequest);
            }

            await LoadProductsAsync();
            if (product is not null)
            {
                SelectProduct(product.Id);
                await LoadAnalyticsAsync(product.Id, product.Name);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Товары", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectProduct(int productId)
    {
        var item = _products.FirstOrDefault(x => x.Id == productId);
        if (item is not null)
        {
            ProductsDataGrid.SelectedItem = item;
            ProductsDataGrid.ScrollIntoView(item);
        }
    }

    private void NewProductButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetEditor();
        ProductsDataGrid.UnselectAll();
    }

    private void ResetEditorButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetEditor();
        ProductsDataGrid.UnselectAll();
    }

    private async void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        await LoadLookupsAsync();
        await LoadProductsAsync();
    }

    private async void RecalculateAiButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _aiService.RefreshModelsAsync();
            await LoadProductsAsync();

            if (_selectedProductId.HasValue)
            {
                await LoadAnalyticsAsync(_selectedProductId.Value, NameTextBox.Text.Trim());
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Товары", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void NameTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _isApplyingSuggestedCategory)
        {
            return;
        }

        var suggestion = await _aiService.SuggestCategoryAsync(NameTextBox.Text);
        SuggestionTextBlock.Text = suggestion is null
            ? "AI-рекомендация появится после обучения модели."
            : suggestion.IsStrongRecommendation
                ? $"AI рекомендует категорию: {suggestion.CategoryName} ({suggestion.Confidence:P0})."
                : $"Слабая рекомендация: {suggestion.CategoryName} ({suggestion.Confidence:P0}).";

        ApplySuggestedCategoryButton.IsEnabled = suggestion is not null;
        ApplySuggestedCategoryButton.Tag = suggestion;
        IgnoreSuggestedCategoryButton.IsEnabled = suggestion is not null;
    }

    private void ApplySuggestedCategoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ApplySuggestedCategoryButton.Tag is not AiCategoryRecommendationDto recommendation)
        {
            return;
        }

        _isApplyingSuggestedCategory = true;
        try
        {
            CategoryEditorComboBox.SelectedValue = recommendation.CategoryId;
        }
        finally
        {
            _isApplyingSuggestedCategory = false;
        }
    }

    private void IgnoreSuggestedCategoryButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplySuggestedCategoryButton.Tag = null;
        ApplySuggestedCategoryButton.IsEnabled = false;
        IgnoreSuggestedCategoryButton.IsEnabled = false;
        SuggestionTextBlock.Text = "Рекомендация категории скрыта.";
    }

    private void ResetEditor()
    {
        _selectedProductId = null;
        EditorTitleText.Text = "Новый товар";
        SkuTextBox.Text = string.Empty;
        NameTextBox.Text = string.Empty;
        UnitTextBox.Text = "шт";
        CurrentStockTextBox.Text = "0";
        MinStockTextBox.Text = "0";
        PurchasePriceTextBox.Text = "0";
        SalePriceTextBox.Text = "0";
        SuggestionTextBlock.Text = "AI-рекомендация появится после обучения модели.";
        AnalyticsSummaryText.Text = "Выбери товар, чтобы увидеть историю и прогноз.";
        AnalyticsMetaText.Text = string.Empty;
        _forecastPoints.Clear();
        ApplySuggestedCategoryButton.Tag = null;
        ApplySuggestedCategoryButton.IsEnabled = false;
        IgnoreSuggestedCategoryButton.IsEnabled = false;

        if (CategoryEditorComboBox.Items.Count > 0) CategoryEditorComboBox.SelectedIndex = 0;
        if (SupplierEditorComboBox.Items.Count > 0) SupplierEditorComboBox.SelectedIndex = 0;
        if (LocationEditorComboBox.Items.Count > 0) LocationEditorComboBox.SelectedIndex = 0;
    }

    private async void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadProductsAsync();
    }

    private async void FilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadProductsAsync();
    }

    private async void CriticalOnlyCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await LoadProductsAsync();
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out value);
    }
}
