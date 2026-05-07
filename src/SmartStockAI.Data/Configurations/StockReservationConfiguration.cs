using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("StockReservations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasPrecision(18, 3);

        builder.Property(x => x.Reference)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Comment)
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Product)
            .WithMany(x => x.StockReservations)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
