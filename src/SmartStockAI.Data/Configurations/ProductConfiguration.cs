using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sku)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Unit)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.CurrentStock)
            .HasPrecision(18, 3);

        builder.Property(x => x.ReservedStock)
            .HasPrecision(18, 3);

        builder.Property(x => x.MinStock)
            .HasPrecision(18, 3);

        builder.Property(x => x.PurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.SalePrice)
            .HasPrecision(18, 2);

        builder.HasIndex(x => x.Sku)
            .IsUnique();

        builder.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Supplier)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Location)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
