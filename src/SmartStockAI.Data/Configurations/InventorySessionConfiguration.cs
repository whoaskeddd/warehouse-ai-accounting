using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class InventorySessionConfiguration : IEntityTypeConfiguration<InventorySession>
{
    public void Configure(EntityTypeBuilder<InventorySession> builder)
    {
        builder.ToTable("InventorySessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.Property(x => x.StartedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.Number)
            .IsUnique();

        builder.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.StartedInventorySessions)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CompletedByUser)
            .WithMany(x => x.CompletedInventorySessions)
            .HasForeignKey(x => x.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
