using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Navigation;

namespace SmartStockAI.App;

public partial class MainWindow : Window
{
    private readonly NavigationService _navigationService;

    public MainWindow()
    {
        InitializeComponent();
        _navigationService = new NavigationService(MainFrame);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _navigationService.Navigate("Dashboard");
    }

    private void NavigationList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavigationList.SelectedItem is not ListBoxItem item || item.Tag is not string key)
        {
            return;
        }

        
    }
}
