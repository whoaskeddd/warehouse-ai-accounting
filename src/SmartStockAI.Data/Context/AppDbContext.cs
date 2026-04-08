using Microsoft.EntityFrameworkCore;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Context;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<User> Users => Set<User>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockDocumentLine> StockDocumentLines => Set<StockDocumentLine>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<InventorySession> InventorySessions => Set<InventorySession>();
    public DbSet<InventorySessionLine> InventorySessionLines => Set<InventorySessionLine>();
    public DbSet<DiscrepancyReport> DiscrepancyReports => Set<DiscrepancyReport>();
    public DbSet<BackupEntry> BackupEntries => Set<BackupEntry>();
    public DbSet<ImportedReportSnapshot> ImportedReportSnapshots => Set<ImportedReportSnapshot>();
    public DbSet<ForecastSnapshot> ForecastSnapshots => Set<ForecastSnapshot>();
    public DbSet<ModelTrainingInfo> ModelTrainingInfos => Set<ModelTrainingInfo>();
    public DbSet<ExpectedInboundSnapshot> ExpectedInboundSnapshots => Set<ExpectedInboundSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
