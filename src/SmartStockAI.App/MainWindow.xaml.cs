using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Navigation;

namespace SmartStockAI.App;

public partial class MainWindow : Window
{
    private NavigationService? _navigationService;

    public MainWindow()
    {
        InitializeComponent();
        _navigationService = new NavigationService(MainFrame);
        NavigationList.SelectedIndex = 0;
        _navigationService.Navigate("Dashboard");
    }

    private void NavigationList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_navigationService is null)
        {
            return;
        }

        if (NavigationList.SelectedItem is not ListBoxItem item || item.Tag is not string key)
        {
            return;
        }

        
    }
}
