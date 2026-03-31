using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Suppliers;

namespace SmartStockAI.App.Views;

public partial class SuppliersPage : Page
{
    private readonly ISupplierService _supplierService;
    private readonly IProductService _productService;
    private readonly ObservableCollection<SupplierListItem> _suppliers = [];
    private int? _selectedSupplierId;

    public SuppliersPage(ISupplierService supplierService, IProductService productService)
    {
        _supplierService = supplierService;
        _productService = productService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SuppliersDataGrid.ItemsSource is null)
        {
            SuppliersDataGrid.ItemsSource = _suppliers;
        }

        await LoadSuppliersAsync();
        ResetEditor();
    }

    private async Task LoadSuppliersAsync()
    {
        var suppliers = await _supplierService.GetAllAsync();
        var products = await _productService.GetAllAsync();
        var productCounts = products
            .Where(x => x.SupplierId.HasValue)
            .GroupBy(x => x.SupplierId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        var items = suppliers
            .OrderBy(x => x.Name)
            .Select(x => new SupplierListItem
            {
                Id = x.Id,
                Name = x.Name,
                ContactInfo = x.ContactInfo ?? string.Empty,
                ProductCount = productCounts.GetValueOrDefault(x.Id)
            })
            .ToList();

        _suppliers.Clear();
        foreach (var item in items)
        {
            _suppliers.Add(item);
        }
    }

    private async void SuppliersDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SuppliersDataGrid.SelectedItem is not SupplierListItem item)
        {
            return;
        }

        var supplier = await _supplierService.GetByIdAsync(item.Id);
        if (supplier is null)
        {
            return;
        }

        _selectedSupplierId = supplier.Id;
        SupplierEditorTitleText.Text = supplier.Name;
        SupplierNameTextBox.Text = supplier.Name;
        SupplierContactTextBox.Text = supplier.ContactInfo ?? string.Empty;
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SupplierNameTextBox.Text))
        {
            MessageBox.Show("У поставщика должно быть название.", "Поставщики", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SupplierDto? supplier;
            if (_selectedSupplierId.HasValue)
            {
                supplier = await _supplierService.UpdateAsync(_selectedSupplierId.Value, new UpdateSupplierRequest
                {
                    Name = SupplierNameTextBox.Text.Trim(),
                    ContactInfo = SupplierContactTextBox.Text.Trim()
                });
            }
            else
            {
                supplier = await _supplierService.CreateAsync(new CreateSupplierRequest
                {
                    Name = SupplierNameTextBox.Text.Trim(),
                    ContactInfo = SupplierContactTextBox.Text.Trim()
                });
            }

            await LoadSuppliersAsync();
            if (supplier is not null)
            {
                SelectSupplier(supplier.Id);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Поставщики", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectSupplier(int supplierId)
    {
        var item = _suppliers.FirstOrDefault(x => x.Id == supplierId);
        if (item is not null)
        {
            SuppliersDataGrid.SelectedItem = item;
            SuppliersDataGrid.ScrollIntoView(item);
        }
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetEditor();
        SuppliersDataGrid.UnselectAll();
    }

    private async void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        await LoadSuppliersAsync();
    }

    private void ResetEditor()
    {
        _selectedSupplierId = null;
        SupplierEditorTitleText.Text = "Новый поставщик";
        SupplierNameTextBox.Text = string.Empty;
        SupplierContactTextBox.Text = string.Empty;
    }
}
