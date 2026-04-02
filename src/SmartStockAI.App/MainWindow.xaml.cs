using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SmartStockAI.App.Navigation;

namespace SmartStockAI.App;

public partial class MainWindow : Window
{
    private readonly NavigationService _navigationService;
    private bool _isSynchronizingNavigationSelection;

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _navigationService = new NavigationService(
            MainFrame,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());

        NavigationList.SelectedIndex = 0;
        _navigationService.Navigate("Dashboard");
    }

    private void NavigationList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingNavigationSelection)
        {
            return;
        }

        if (NavigationList.SelectedItem is not ListBoxItem item || item.Tag is not string key)
        {
            return;
        }

        NavigateTo(key);
    }

    private void OpenProductsButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigateTo("Products");
    }

    public void NavigateTo(string key)
    {
        _navigationService.Navigate(key);

        _isSynchronizingNavigationSelection = true;
        try
        {
            foreach (var item in NavigationList.Items)
            {
                if (item is ListBoxItem listBoxItem && string.Equals(listBoxItem.Tag as string, key, StringComparison.Ordinal))
                {
                    NavigationList.SelectedItem = listBoxItem;
                    break;
                }
            }
        }
        finally
        {
            _isSynchronizingNavigationSelection = false;
        }
    }
}
