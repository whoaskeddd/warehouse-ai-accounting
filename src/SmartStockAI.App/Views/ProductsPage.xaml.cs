using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.App.Models;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.App.Views;

public partial class ProductsPage : Page
{
    private readonly ObservableCollection<ProductListItem> _products = [];
    private List<LookupItem> _categories = [];
    private List<LookupItem> _suppliers = [];
    private List<LookupItem> _locations = [];
    private int? _selectedProductId;

    public ProductsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ProductsDataGrid.ItemsSource is null)
        {
            ProductsDataGrid.ItemsSource = _products;
        }

        LoadLookups();
        LoadProducts();
        ResetEditor();
    }

    private void LoadLookups()
    {
        using var db = AppDbContextProvider.Create();

        _categories = db.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        _suppliers = db.Suppliers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        _locations = db.Locations
            .AsNoTracking()
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

    private void LoadProducts()
    {
        using var db = AppDbContextProvider.Create();

        var query = db.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Supplier)
            .Include(x => x.Location)
            .AsQueryable();

        var search = SearchBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search) || x.Sku.Contains(search));
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
                CategoryName = x.Category != null ? x.Category.Name : "Без категории",
                SupplierName = x.Supplier != null ? x.Supplier.Name : "Без поставщика",
                LocationName = x.Location != null ? x.Location.Name : "Без локации",
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

    private void ProductsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProductsDataGrid.SelectedItem is not ProductListItem item)
        {
            return;
        }

        using var db = AppDbContextProvider.Create();
        var product = db.Products.AsNoTracking().FirstOrDefault(x => x.Id == item.Id);
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

    private void SaveProductButton_OnClick(object sender, RoutedEventArgs e)
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

        using var db = AppDbContextProvider.Create();

        Product product;
        if (_selectedProductId.HasValue)
        {
            product = db.Products.First(x => x.Id == _selectedProductId.Value);
        }
        else
        {
            product = new Product();
            db.Products.Add(product);
        }

        product.Sku = SkuTextBox.Text.Trim();
        product.Name = NameTextBox.Text.Trim();
        product.Unit = string.IsNullOrWhiteSpace(UnitTextBox.Text) ? "шт" : UnitTextBox.Text.Trim();
        product.CurrentStock = currentStock;
        product.MinStock = minStock;
        product.PurchasePrice = purchasePrice;
        product.SalePrice = salePrice;
        product.CategoryId = CategoryEditorComboBox.SelectedValue as int?;
        product.SupplierId = SupplierEditorComboBox.SelectedValue as int?;
        product.LocationId = LocationEditorComboBox.SelectedValue as int?;

        db.SaveChanges();

        LoadProducts();
        SelectProduct(product.Id);
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

    private void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadLookups();
        LoadProducts();
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

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        LoadProducts();
    }

    private void FilterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        LoadProducts();
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out value);
    }
}
