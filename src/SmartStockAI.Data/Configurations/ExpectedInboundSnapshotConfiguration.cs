using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class ExpectedInboundSnapshotConfiguration : IEntityTypeConfiguration<ExpectedInboundSnapshot>
{
    public void Configure(EntityTypeBuilder<ExpectedInboundSnapshot> builder)
    {
        builder.ToTable("ExpectedInboundSnapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.ExpectedInboundSnapshots)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ProductId)
            .IsUnique();
    }
}
