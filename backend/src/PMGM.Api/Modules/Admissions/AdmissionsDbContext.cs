using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Admissions.Entities;

namespace PMGM.Api.Modules.Admissions;

public sealed class AdmissionsDbContext(DbContextOptions<AdmissionsDbContext> options) : DbContext(options)
{
    public DbSet<AdmissionCase> AdmissionCases => Set<AdmissionCase>();
    public DbSet<AdmissionEvidence> AdmissionEvidence => Set<AdmissionEvidence>();
    public DbSet<AdmissionDecision> AdmissionDecisions => Set<AdmissionDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<AdmissionCase>(entity =>
        {
            entity.ToTable("admission_cases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AdmissionType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AffiliationMode).HasMaxLength(40);
            entity.Property(x => x.OriginLodgeName).HasMaxLength(240);
            entity.Property(x => x.OriginLodgeNumber).HasMaxLength(80);
            entity.Property(x => x.OriginObedience).HasMaxLength(240);
            entity.Property(x => x.Degree).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.AdmissionType, x.Status });
            entity.HasIndex(x => x.PersonId);
            entity.HasIndex(x => x.MemberId);
        });

        modelBuilder.Entity<AdmissionEvidence>(entity =>
        {
            entity.ToTable("admission_evidence");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EvidenceType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.ReviewStatus).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReviewedBySubject).HasMaxLength(320);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.AdmissionCase)
                .WithMany(x => x.Evidence)
                .HasForeignKey(x => x.AdmissionCaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.AdmissionCaseId, x.EvidenceType, x.CreatedAtUtc });
            entity.HasIndex(x => x.DocumentVersionId);
        });

        modelBuilder.Entity<AdmissionDecision>(entity =>
        {
            entity.ToTable("admission_decisions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DecisionType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.AdmissionCase)
                .WithMany(x => x.Decisions)
                .HasForeignKey(x => x.AdmissionCaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.AdmissionCaseId, x.DecisionType, x.RecordedAtUtc });
        });
    }
}
