using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class ForecastSnapshotConfiguration : IEntityTypeConfiguration<ForecastSnapshot>
{
    public void Configure(EntityTypeBuilder<ForecastSnapshot> builder)
    {
        builder.ToTable("ForecastSnapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ScopeType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.ScopeName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.SourceScopeType)
            .HasMaxLength(32);

        builder.Property(x => x.SourceScopeName)
            .HasMaxLength(256);

        builder.Property(x => x.AverageMonthlyDemand)
            .HasPrecision(18, 3);

        builder.Property(x => x.ForecastLeadTime)
            .HasPrecision(18, 3);

        builder.Property(x => x.SafetyStock)
            .HasPrecision(18, 3);

        builder.Property(x => x.ExpectedInbound)
            .HasPrecision(18, 3);

        builder.Property(x => x.RecommendedOrder)
            .HasPrecision(18, 3);

        builder.Property(x => x.ProjectedDeficit)
            .HasPrecision(18, 3);

        builder.Property(x => x.ModelQuality)
            .HasPrecision(18, 6);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.ArtifactPath)
            .IsRequired()
            .HasMaxLength(512);

        builder.HasIndex(x => new { x.ScopeType, x.ScopeId })
            .IsUnique();
    }
}
