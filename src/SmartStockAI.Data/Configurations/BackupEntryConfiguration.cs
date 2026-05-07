using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.Data.Configurations;

public class BackupEntryConfiguration : IEntityTypeConfiguration<BackupEntry>
{
    public void Configure(EntityTypeBuilder<BackupEntry> builder)
    {
        builder.ToTable("BackupEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.FullPath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedBackups)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RestoredByUser)
            .WithMany(x => x.RestoredBackups)
            .HasForeignKey(x => x.RestoredByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
