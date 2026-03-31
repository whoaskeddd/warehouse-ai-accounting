using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SmartStockAI.App.Navigation;

namespace SmartStockAI.App;

public partial class MainWindow : Window
{
    private readonly NavigationService _navigationService;

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
        if (NavigationList.SelectedItem is not ListBoxItem item || item.Tag is not string key)
        {
            return;
        }

        _navigationService.Navigate(key);
    }
}
