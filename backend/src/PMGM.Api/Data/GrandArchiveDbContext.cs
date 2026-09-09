using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.GrandArchive.Entities;

namespace PMGM.Api.Data;

public sealed class GrandArchiveDbContext(DbContextOptions<GrandArchiveDbContext> options) : DbContext(options)
{
    public DbSet<GrandArchiveRecord> GrandArchiveRecords => Set<GrandArchiveRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");
        modelBuilder.Entity<GrandArchiveRecord>(entity =>
        {
            entity.ToTable("grand_archive_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ArchiveCode).HasMaxLength(120).IsRequired();
            entity.Property(x => x.RecordType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.OriginatingBody).HasMaxLength(240);
            entity.Property(x => x.HistoricalPeriod).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedByDisplayName).HasMaxLength(320);
            entity.Property(x => x.ArchivedAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => x.ArchiveCode).IsUnique();
            entity.HasIndex(x => x.DocumentVersionId).IsUnique();
            entity.HasIndex(x => new { x.Status, x.DocumentDate });
            entity.HasIndex(x => new { x.RecordType, x.Status });
        });
    }
}
