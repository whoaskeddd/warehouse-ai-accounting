using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class ImportedReportSnapshotConfiguration : IEntityTypeConfiguration<ImportedReportSnapshot>
{
    public void Configure(EntityTypeBuilder<ImportedReportSnapshot> builder)
    {
        builder.ToTable("ImportedReportSnapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReportKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.ReportName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.SourceFileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(x => x.ImportedByDisplayName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Summary)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.HasIndex(x => x.ImportedAtUtc);
        builder.HasIndex(x => x.ReportKey);
    }
}
