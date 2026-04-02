using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3);

        builder.Property(x => x.BalanceAfter)
            .HasPrecision(18, 3);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.DocumentNumber)
            .HasMaxLength(64);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StockDocument)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.StockDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Reservation)
            .WithMany(x => x.StockMovements)
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
