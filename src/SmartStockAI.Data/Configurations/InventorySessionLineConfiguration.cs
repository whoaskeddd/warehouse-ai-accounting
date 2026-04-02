using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class InventorySessionLineConfiguration : IEntityTypeConfiguration<InventorySessionLine>
{
    public void Configure(EntityTypeBuilder<InventorySessionLine> builder)
    {
        builder.ToTable("InventorySessionLines");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExpectedStock)
            .HasPrecision(18, 3);

        builder.Property(x => x.ActualStock)
            .HasPrecision(18, 3);

        builder.Property(x => x.Variance)
            .HasPrecision(18, 3);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.HasOne(x => x.InventorySession)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.InventorySessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.InventorySessionId, x.ProductId })
            .IsUnique();
    }
}
