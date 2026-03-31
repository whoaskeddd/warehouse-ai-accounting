using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartStockAI.Data.Context;
using System.Windows;

namespace SmartStockAI.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=smartstockai.db";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        using var dbContext = new AppDbContext(options);
        dbContext.Database.Migrate();

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
