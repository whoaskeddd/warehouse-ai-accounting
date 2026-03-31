using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartStockAI.Data.Context;

namespace SmartStockAI.App.Services;

public static class AppDbContextProvider
{
    public static AppDbContext Create()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=smartstockai.db";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
