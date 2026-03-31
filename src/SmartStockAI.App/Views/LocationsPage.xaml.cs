using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.App.Models;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.App.Views;

public partial class LocationsPage : Page
{
    private readonly ObservableCollection<LocationListItem> _locations = [];
    private int? _selectedLocationId;

    public LocationsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (LocationsDataGrid.ItemsSource is null)
        {
            LocationsDataGrid.ItemsSource = _locations;
        }

        LoadLocations();
        ResetEditor();
    }

    private void LoadLocations()
    {
        using var db = AppDbContextProvider.Create();

        var lookup = db.Locations.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        ParentLocationComboBox.ItemsSource = CreateLookupOptions("Корневая локация", lookup);
        if (!_selectedLocationId.HasValue)
        {
            ParentLocationComboBox.SelectedIndex = 0;
        }

        var items = db.Locations
            .AsNoTracking()
            .Include(x => x.ParentLocation)
            .Include(x => x.Products)
            .OrderBy(x => x.Name)
            .Select(x => new LocationListItem
            {
                Id = x.Id,
                Name = x.Name,
                ParentName = x.ParentLocation != null ? x.ParentLocation.Name : "Корень",
                ProductCount = x.Products.Count
            })
            .ToList();

        _locations.Clear();
        foreach (var item in items)
        {
            _locations.Add(item);
        }
    }

    private void LocationsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocationsDataGrid.SelectedItem is not LocationListItem item)
        {
            return;
        }

        using var db = AppDbContextProvider.Create();
        var location = db.Locations.AsNoTracking().FirstOrDefault(x => x.Id == item.Id);
        if (location is null)
        {
            return;
        }

        _selectedLocationId = location.Id;
        LocationEditorTitleText.Text = location.Name;
        LocationNameTextBox.Text = location.Name;
        ParentLocationComboBox.SelectedValue = location.ParentLocationId;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LocationNameTextBox.Text))
        {
            MessageBox.Show("У локации должно быть название.", "Локации", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = AppDbContextProvider.Create();
        Location location;
        if (_selectedLocationId.HasValue)
        {
            location = db.Locations.First(x => x.Id == _selectedLocationId.Value);
        }
        else
        {
            location = new Location();
            db.Locations.Add(location);
        }

        location.Name = LocationNameTextBox.Text.Trim();
        location.ParentLocationId = ParentLocationComboBox.SelectedValue as int?;
        db.SaveChanges();

        LoadLocations();
        SelectLocation(location.Id);
    }

    private void SelectLocation(int locationId)
    {
        var item = _locations.FirstOrDefault(x => x.Id == locationId);
        if (item is not null)
        {
            LocationsDataGrid.SelectedItem = item;
            LocationsDataGrid.ScrollIntoView(item);
        }
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetEditor();
        LocationsDataGrid.UnselectAll();
    }

    private void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadLocations();
    }

    private void ResetEditor()
    {
        _selectedLocationId = null;
        LocationEditorTitleText.Text = "Новая локация";
        LocationNameTextBox.Text = string.Empty;
        if (ParentLocationComboBox.Items.Count > 0)
        {
            ParentLocationComboBox.SelectedIndex = 0;
        }
    }

    private static List<LookupItem> CreateLookupOptions(string emptyTitle, IEnumerable<LookupItem> items)
    {
        return [new LookupItem { Id = null, Name = emptyTitle }, .. items];
    }
}
