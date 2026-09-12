using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Data;

public sealed class RegimenInteriorDbContext(DbContextOptions<RegimenInteriorDbContext> options) : DbContext(options)
{
    public DbSet<DataQualityCase> DataQualityCases => Set<DataQualityCase>();
    public DbSet<DataQualityCaseEvent> DataQualityCaseEvents => Set<DataQualityCaseEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<DataQualityCase>(entity =>
        {
            entity.ToTable("data_quality_cases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IssueFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RuleCode).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Severity).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AssignedToSubject).HasMaxLength(320);
            entity.Property(x => x.AssignedToDisplayName).HasMaxLength(320);
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedByDisplayName).HasMaxLength(320);
            entity.Property(x => x.ResolutionSummary).HasMaxLength(2000);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.ResolvedBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Status, x.UpdatedAtUtc });
            entity.HasIndex(x => new { x.MemberId, x.Status });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.HasIndex(x => x.IssueFingerprint)
                .IsUnique()
                .HasFilter("\"Status\" IN ('open','under_review')");
        });

        modelBuilder.Entity<DataQualityCaseEvent>(entity =>
        {
            entity.ToTable("data_quality_case_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(80).IsRequired();
            entity.Property(x => x.FromStatus).HasMaxLength(40);
            entity.Property(x => x.ToStatus).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ActorSubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ActorDisplayName).HasMaxLength(320);
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.HasOne(x => x.DataQualityCase)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.DataQualityCaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.DataQualityCaseId, x.OccurredAtUtc });
        });
    }
}
