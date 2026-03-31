using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using SmartStockAI.App.Views;

namespace SmartStockAI.App.Navigation;

public sealed class NavigationService
{
    private readonly Frame _hostFrame;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Dictionary<string, Type> _routes;
    private IServiceScope? _currentScope;

    public NavigationService(Frame hostFrame, IServiceScopeFactory scopeFactory)
    {
        _hostFrame = hostFrame;
        _scopeFactory = scopeFactory;
        _routes = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Dashboard"] = typeof(DashboardPage),
            ["Products"] = typeof(ProductsPage),
            ["Categories"] = typeof(CategoriesPage),
            ["Suppliers"] = typeof(SuppliersPage),
            ["Locations"] = typeof(LocationsPage),
            ["Inbound"] = typeof(InboundPage),
            ["Outbound"] = typeof(OutboundPage),
            ["Inventory"] = typeof(InventoryPage),
            ["Reports"] = typeof(ReportsPage)
        };
    }

    public void Navigate(string key)
    {
        if (!_routes.TryGetValue(key, out var pageType))
        {
            return;
        }

        var scope = _scopeFactory.CreateScope();
        var page = (Page)scope.ServiceProvider.GetRequiredService(pageType);
        var previousScope = _currentScope;

        _hostFrame.Navigate(page);
        _currentScope = scope;
        previousScope?.Dispose();
    }
}
