using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.Locations;
using SmartStockAI.Core.Contracts.Products;

namespace SmartStockAI.App.Views;

public partial class LocationsPage : Page
{
    private readonly ILocationService _locationService;
    private readonly IProductService _productService;
    private readonly ObservableCollection<LocationListItem> _locations = [];
    private int? _selectedLocationId;

    public LocationsPage(ILocationService locationService, IProductService productService)
    {
        _locationService = locationService;
        _productService = productService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (LocationsDataGrid.ItemsSource is null)
        {
            LocationsDataGrid.ItemsSource = _locations;
        }

        await LoadLocationsAsync();
        ResetEditor();
    }

    private async Task LoadLocationsAsync()
    {
        var locations = await _locationService.GetAllAsync();
        var products = await _productService.GetAllAsync();
        var productCounts = products
            .Where(x => x.LocationId.HasValue)
            .GroupBy(x => x.LocationId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        var lookup = locations
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        ParentLocationComboBox.ItemsSource = CreateLookupOptions("Корневая локация", lookup);
        if (!_selectedLocationId.HasValue)
        {
            ParentLocationComboBox.SelectedIndex = 0;
        }

        var items = locations
            .OrderBy(x => x.Name)
            .Select(x => new LocationListItem
            {
                Id = x.Id,
                Name = x.Name,
                ParentName = x.ParentLocationName ?? "Корень",
                ProductCount = productCounts.GetValueOrDefault(x.Id)
            })
            .ToList();

        _locations.Clear();
        foreach (var item in items)
        {
            _locations.Add(item);
        }
    }

    private async void LocationsDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocationsDataGrid.SelectedItem is not LocationListItem item)
        {
            return;
        }

        var location = await _locationService.GetByIdAsync(item.Id);
        if (location is null)
        {
            return;
        }

        _selectedLocationId = location.Id;
        LocationEditorTitleText.Text = location.Name;
        LocationNameTextBox.Text = location.Name;
        ParentLocationComboBox.SelectedValue = location.ParentLocationId;
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LocationNameTextBox.Text))
        {
            MessageBox.Show("У локации должно быть название.", "Локации", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            LocationDto? location;
            if (_selectedLocationId.HasValue)
            {
                location = await _locationService.UpdateAsync(_selectedLocationId.Value, new UpdateLocationRequest
                {
                    Name = LocationNameTextBox.Text.Trim(),
                    ParentLocationId = ParentLocationComboBox.SelectedValue as int?
                });
            }
            else
            {
                location = await _locationService.CreateAsync(new CreateLocationRequest
                {
                    Name = LocationNameTextBox.Text.Trim(),
                    ParentLocationId = ParentLocationComboBox.SelectedValue as int?
                });
            }

            await LoadLocationsAsync();
            if (location is not null)
            {
                SelectLocation(location.Id);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Локации", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    private async void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        await LoadLocationsAsync();
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
