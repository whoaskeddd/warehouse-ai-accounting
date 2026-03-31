using System;
using System.Collections.Generic;
using System.Windows.Controls;
using SmartStockAI.App.Views;

namespace SmartStockAI.App.Navigation;

public sealed class NavigationService
{
    private readonly Frame _hostFrame;
    private readonly Dictionary<string, Func<Page>> _routes;

    public NavigationService(Frame hostFrame)
    {
        _hostFrame = hostFrame;
        _routes = new Dictionary<string, Func<Page>>(StringComparer.Ordinal)
        {
            ["Dashboard"] = static () => new DashboardPage(),
            ["Products"] = static () => new ProductsPage(),
            ["Categories"] = static () => new CategoriesPage(),
            ["Suppliers"] = static () => new SuppliersPage(),
            ["Locations"] = static () => new LocationsPage(),
            ["Inbound"] = static () => new InboundPage(),
            ["Outbound"] = static () => new OutboundPage(),
            ["Inventory"] = static () => new InventoryPage(),
            ["Reports"] = static () => new ReportsPage()
        };
    }

    public void Navigate(string key)
    {
        if (!_routes.TryGetValue(key, out var factory))
        {
            return;
        }

        _hostFrame.Navigate(factory());
    }
}
