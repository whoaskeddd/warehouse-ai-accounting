using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartStockAI.Core.Contracts.Categories;
using SmartStockAI.Core.Contracts.Locations;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Suppliers;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Services;
using System.Windows;

namespace SmartStockAI.App;

public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services =>
        _host?.Services ?? throw new InvalidOperationException("Application host is not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.SetBasePath(AppContext.BaseDirectory);
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                    ?? "Data Source=smartstockai.db";

                services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
                services.AddScoped<IProductService, ProductService>();
                services.AddScoped<ICategoryService, CategoryService>();
                services.AddScoped<ISupplierService, SupplierService>();
                services.AddScoped<ILocationService, LocationService>();
                services.AddTransient<MainWindow>();
                services.AddTransient<Views.ProductsPage>();
                services.AddTransient<Views.CategoriesPage>();
                services.AddTransient<Views.SuppliersPage>();
                services.AddTransient<Views.LocationsPage>();
                services.AddTransient<Views.DashboardPage>();
                services.AddTransient<Views.InboundPage>();
                services.AddTransient<Views.OutboundPage>();
                services.AddTransient<Views.InventoryPage>();
                services.AddTransient<Views.ReportsPage>();
            })
            .Build();

        using var scope = Services.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
