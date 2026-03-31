using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.Categories;
using SmartStockAI.Core.Contracts.Locations;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Suppliers;

namespace SmartStockAI.App.Views;

public partial class ProductsPage : Page
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ISupplierService _supplierService;
    private readonly ILocationService _locationService;
    private readonly ObservableCollection<ProductListItem> _products = [];
    private List<LookupItem> _categories = [];
    private List<LookupItem> _suppliers = [];
    private List<LookupItem> _locations = [];
    private int? _selectedProductId;

    public ProductsPage(
        IProductService productService,
        ICategoryService categoryService,
        ISupplierService supplierService,
        ILocationService locationService)
    {
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
        IEnumerable<ProductDto> query = await _productService.GetAllAsync();

        var search = SearchBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Sku.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (CategoryFilterComboBox.SelectedValue is int categoryId)
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        if (SupplierFilterComboBox.SelectedValue is int supplierId)
        {
            query = query.Where(x => x.SupplierId == supplierId);
        }

        var items = query
            .OrderBy(x => x.Name)
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

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out value);
    }
}
