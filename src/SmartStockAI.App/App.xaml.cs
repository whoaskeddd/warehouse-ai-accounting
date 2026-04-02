using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartStockAI.Core.Contracts.Auth;
using SmartStockAI.Core.Contracts.Audit;
using SmartStockAI.Core.Contracts.Backup;
using SmartStockAI.Core.Contracts.Categories;
using SmartStockAI.Core.Contracts.Inventory;
using SmartStockAI.Core.Contracts.Locations;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Stock;
using SmartStockAI.Core.Contracts.Suppliers;
using SmartStockAI.Core.Contracts.Users;
using SmartStockAI.Data.Context;
using SmartStockAI.Data.Security;
using SmartStockAI.Data.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SmartStockAI.App;

public partial class App : Application
{
    private IHost? _host;

    public IServiceProvider Services =>
        _host?.Services ?? throw new InvalidOperationException("Application host is not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EventManager.RegisterClassHandler(
            typeof(DataGrid),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(HandleDataGridPreviewMouseWheel),
            handledEventsToo: true);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.SetBasePath(AppContext.BaseDirectory);
                configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                var connectionString = SqliteConnectionStringResolver.Resolve(
                    context.Configuration.GetConnectionString("DefaultConnection"));

                services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
                services.AddSingleton<ICurrentUserAccessor, CurrentUserAccessor>();
                services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
                services.AddScoped<AppDataInitializer>();
                services.AddScoped<IAuditService, AuditService>();
                services.AddScoped<IAuditLogWriter>(provider => (AuditService)provider.GetRequiredService<IAuditService>());
                services.AddScoped<IAuthService, AuthService>();
                services.AddScoped<IUserService, UserService>();
                services.AddScoped<IInventoryService, InventoryService>();
                services.AddScoped<IBackupService, BackupService>();
                services.AddScoped<IProductService, ProductService>();
                services.AddScoped<ICategoryService, CategoryService>();
                services.AddScoped<ISupplierService, SupplierService>();
                services.AddScoped<ILocationService, LocationService>();
                services.AddScoped<IStockService, StockService>();
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
        var initializer = scope.ServiceProvider.GetRequiredService<AppDataInitializer>();
        initializer.InitializeAsync().GetAwaiter().GetResult();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        authService.LoginAsync(new LoginRequest
        {
            Login = DefaultAdminCredentials.Login,
            Password = DefaultAdminCredentials.Password
        }).GetAwaiter().GetResult();

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

    private static void HandleDataGridPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid dataGrid)
        {
            return;
        }

        var parentScrollViewer = FindAncestor<ScrollViewer>(dataGrid);
        if (parentScrollViewer is null)
        {
            return;
        }

        e.Handled = true;
        var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = dataGrid
        };

        parentScrollViewer.RaiseEvent(forwardedEvent);
    }

    private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
