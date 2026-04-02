using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SmartStockAI.App.Navigation;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Contracts.Auth;
using SmartStockAI.Core.Enums;

namespace SmartStockAI.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NavigationService _navigationService;
    private readonly IAuthService _authService;
    private readonly AppSessionService _appSession;
    private bool _isSynchronizingNavigationSelection;

    public MainWindow(IServiceProvider serviceProvider, AppSessionService appSession, IAuthService authService)
    {
        _serviceProvider = serviceProvider;
        _appSession = appSession;
        _authService = authService;

        InitializeComponent();
        DataContext = this;

        _navigationService = new NavigationService(
            MainFrame,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());

        _appSession.CurrentUserChanged += AppSession_OnCurrentUserChanged;

        ApplyRoleNavigation();
        NavigateTo(GetDefaultRoute());
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

    private async void LogoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _authService.LogoutAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Выход", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _appSession.SetCurrentUser(null);
        Hide();

        var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
        var loginResult = loginWindow.ShowDialog();
        if (loginResult == true)
        {
            Show();
            Activate();
            ApplyRoleNavigation();
            NavigateTo(GetDefaultRoute());
            return;
        }

        Close();
    }

    public void NavigateTo(string key)
    {
        if (!IsRouteAllowed(key))
        {
            key = GetDefaultRoute();
        }

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
        ApplyRoleNavigation();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentUserDisplay)));
    }

    private void ApplyRoleNavigation()
    {
        foreach (var item in NavigationList.Items)
        {
            if (item is not ListBoxItem listBoxItem || listBoxItem.Tag is not string key)
            {
                continue;
            }

            listBoxItem.Visibility = IsRouteAllowed(key) ? Visibility.Visible : Visibility.Collapsed;
        }

        var role = _appSession.CurrentUser?.Role;
        OpenUsersButton.Visibility = role == UserRole.Admin ? Visibility.Visible : Visibility.Collapsed;
        QuickActionCard.Visibility = role == UserRole.Manager ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool IsRouteAllowed(string key)
    {
        var role = _appSession.CurrentUser?.Role;
        return role switch
        {
            UserRole.Admin => true,
            UserRole.WarehouseOperator => key is not "Users" and not "Administration",
            UserRole.Manager => key is "Dashboard" or "Reports",
            _ => false
        };
    }

    private string GetDefaultRoute()
    {
        return _appSession.CurrentUser?.Role switch
        {
            UserRole.Manager => "Reports",
            UserRole.WarehouseOperator => "Dashboard",
            UserRole.Admin => "Dashboard",
            _ => "Dashboard"
        };
    }
}
