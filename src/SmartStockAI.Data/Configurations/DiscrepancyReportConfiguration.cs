using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class DiscrepancyReportConfiguration : IEntityTypeConfiguration<DiscrepancyReport>
{
    public void Configure(EntityTypeBuilder<DiscrepancyReport> builder)
    {
        builder.ToTable("DiscrepancyReports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.TotalVariance)
            .HasPrecision(18, 3);

        builder.HasIndex(x => x.Number)
            .IsUnique();

        builder.HasOne(x => x.InventorySession)
            .WithOne(x => x.DiscrepancyReport)
            .HasForeignKey<DiscrepancyReport>(x => x.InventorySessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
