using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SmartStockAI.App.Navigation;
using SmartStockAI.App.Services;

namespace SmartStockAI.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly NavigationService _navigationService;
    private readonly AppSessionService _appSession;
    private bool _isSynchronizingNavigationSelection;

    public MainWindow(IServiceProvider serviceProvider, AppSessionService appSession)
    {
        _appSession = appSession;

        InitializeComponent();
        DataContext = this;

        _navigationService = new NavigationService(
            MainFrame,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());

        _appSession.CurrentUserChanged += AppSession_OnCurrentUserChanged;

        NavigationList.SelectedIndex = 0;
        _navigationService.Navigate("Dashboard");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentUserDisplay => _appSession.CurrentUserDisplayName;

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

    private void OpenUsersButton_OnClick(object sender, RoutedEventArgs e)
    {
        NavigateTo("Users");
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

    private void AppSession_OnCurrentUserChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentUserDisplay)));
    }
}
