using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class ModelTrainingInfoConfiguration : IEntityTypeConfiguration<ModelTrainingInfo>
{
    public void Configure(EntityTypeBuilder<ModelTrainingInfo> builder)
    {
        builder.ToTable("ModelTrainingInfos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ModelType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.ScopeType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.QualityMetric)
            .HasPrecision(18, 6);

        builder.Property(x => x.ArtifactPath)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.Notes)
            .HasMaxLength(1024);

        builder.HasIndex(x => new { x.ModelType, x.ScopeType, x.ScopeId });
    }
}
