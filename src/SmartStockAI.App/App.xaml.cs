using Microsoft.EntityFrameworkCore;
using SmartStockAI.App.Services;
using SmartStockAI.Data.Context;
using System.Windows;

namespace SmartStockAI.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        using var dbContext = AppDbContextProvider.Create();
        dbContext.Database.Migrate();
    }
}
