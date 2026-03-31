using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.App.Models;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.App.Views;

public partial class SuppliersPage : Page
{
    private readonly ObservableCollection<SupplierListItem> _suppliers = [];
    private int? _selectedSupplierId;

    public SuppliersPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (SuppliersDataGrid.ItemsSource is null)
        {
            SuppliersDataGrid.ItemsSource = _suppliers;
        }

        LoadSuppliers();
        ResetEditor();
    }

    private void LoadSuppliers()
    {
        using var db = AppDbContextProvider.Create();
        var items = db.Suppliers
            .AsNoTracking()
            .Include(x => x.Products)
            .OrderBy(x => x.Name)
            .Select(x => new SupplierListItem
            {
                Id = x.Id,
                Name = x.Name,
                ContactInfo = x.ContactInfo ?? string.Empty,
                ProductCount = x.Products.Count
            })
            .ToList();

        _suppliers.Clear();
        foreach (var item in items)
        {
            _suppliers.Add(item);
        }
    }

    private void SuppliersDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SuppliersDataGrid.SelectedItem is not SupplierListItem item)
        {
            return;
        }

        using var db = AppDbContextProvider.Create();
        var supplier = db.Suppliers.AsNoTracking().FirstOrDefault(x => x.Id == item.Id);
        if (supplier is null)
        {
            return;
        }

        _selectedSupplierId = supplier.Id;
        SupplierEditorTitleText.Text = supplier.Name;
        SupplierNameTextBox.Text = supplier.Name;
        SupplierContactTextBox.Text = supplier.ContactInfo ?? string.Empty;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SupplierNameTextBox.Text))
        {
            MessageBox.Show("У поставщика должно быть название.", "Поставщики", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = AppDbContextProvider.Create();
        Supplier supplier;
        if (_selectedSupplierId.HasValue)
        {
            supplier = db.Suppliers.First(x => x.Id == _selectedSupplierId.Value);
        }
        else
        {
            supplier = new Supplier();
            db.Suppliers.Add(supplier);
        }

        supplier.Name = SupplierNameTextBox.Text.Trim();
        supplier.ContactInfo = SupplierContactTextBox.Text.Trim();
        db.SaveChanges();

        LoadSuppliers();
        SelectSupplier(supplier.Id);
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

    private void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadSuppliers();
    }

    private void ResetEditor()
    {
        _selectedSupplierId = null;
        SupplierEditorTitleText.Text = "Новый поставщик";
        SupplierNameTextBox.Text = string.Empty;
        SupplierContactTextBox.Text = string.Empty;
    }
}
