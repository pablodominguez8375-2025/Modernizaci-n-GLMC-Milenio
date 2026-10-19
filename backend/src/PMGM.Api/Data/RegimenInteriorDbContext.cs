using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Data;

public sealed class RegimenInteriorDbContext(DbContextOptions<RegimenInteriorDbContext> options) : DbContext(options)
{
    public DbSet<DataQualityCase> DataQualityCases => Set<DataQualityCase>();
    public DbSet<DataQualityCaseEvent> DataQualityCaseEvents => Set<DataQualityCaseEvent>();
    public DbSet<AssemblyRecord> Assemblies => Set<AssemblyRecord>();
    public DbSet<AssemblyMemberStatusRecord> AssemblyMemberStatuses => Set<AssemblyMemberStatusRecord>();
    public DbSet<AssemblyEligibilityRecord> AssemblyEligibilities => Set<AssemblyEligibilityRecord>();
    public DbSet<AssemblyDebtDecisionEventRecord> AssemblyDebtDecisionEvents => Set<AssemblyDebtDecisionEventRecord>();
    public DbSet<InstitutionalRestrictionRecord> InstitutionalRestrictions => Set<InstitutionalRestrictionRecord>();
    public DbSet<AssemblyTreasuryCertificationEventRecord> AssemblyTreasuryCertificationEvents => Set<AssemblyTreasuryCertificationEventRecord>();
    public DbSet<AssemblyTreasurySignedReviewRecord> AssemblyTreasurySignedReviews => Set<AssemblyTreasurySignedReviewRecord>();

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


        modelBuilder.Entity<AssemblyRecord>(entity =>
        {
            entity.ToTable("assemblies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(24).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ClosedBySubject).HasMaxLength(320);
            entity.Property(x => x.ClosureEvidenceDigest).HasMaxLength(96);
            entity.HasIndex(x => new { x.Status, x.CutoffDate });
        });

        modelBuilder.Entity<AssemblyMemberStatusRecord>(entity =>
        {
            entity.ToTable("assembly_member_statuses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SourceEvidenceReference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.LastNamesAtCutoff).HasMaxLength(240).IsRequired();
            entity.Property(x => x.FirstNamesAtCutoff).HasMaxLength(240).IsRequired();
            entity.Property(x => x.RutAtCutoff).HasMaxLength(32).IsRequired();
            entity.Property(x => x.WorkshopAtCutoff).HasMaxLength(240).IsRequired();
            entity.HasOne(x => x.Assembly).WithMany(x => x.Members)
                .HasForeignKey(x => x.AssemblyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasAlternateKey(x => new { x.AssemblyId, x.Id });
            entity.HasIndex(x => new { x.AssemblyId, x.PersonId }).IsUnique();
            entity.HasIndex(x => new { x.AssemblyId, x.OrganizationId });
        });

        modelBuilder.Entity<AssemblyEligibilityRecord>(entity =>
        {
            entity.ToTable("assembly_eligibilities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReasonCode).HasMaxLength(160).IsRequired();
            entity.Property(x => x.GrandTreasuryPendingAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrandTreasuryCurrency).HasMaxLength(12);
            entity.Property(x => x.GrandTreasurySourceReference).HasMaxLength(500);
            entity.Property(x => x.InstitutionalRightsSourceReference).HasMaxLength(500);
            entity.Property(x => x.HonorRightsSourceReference).HasMaxLength(500);
            entity.Property(x => x.EvaluatedBySubject).HasMaxLength(320).IsRequired();
            entity.HasOne(x => x.MemberStatus).WithMany(x => x.EligibilityHistory)
                .HasForeignKey(x => new { x.AssemblyId, x.MemberStatusId })
                .HasPrincipalKey(x => new { x.AssemblyId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AssemblyId, x.MemberStatusId, x.EvaluatedAtUtc });
            entity.HasIndex(x => x.SupersedesEligibilityId).IsUnique()
                .HasFilter("\"SupersedesEligibilityId\" IS NOT NULL");
        });

        modelBuilder.Entity<AssemblyDebtDecisionEventRecord>(entity =>
        {
            entity.ToTable("assembly_debt_decision_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Authority).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DecidedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired();
            entity.HasOne<AssemblyRecord>().WithMany()
                .HasForeignKey(x => x.AssemblyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AssemblyId, x.OrganizationId, x.OccurredAtUtc });
            entity.HasIndex(x => x.ReversesEventId).IsUnique()
                .HasFilter("\"ReversesEventId\" IS NOT NULL");
        });

        modelBuilder.Entity<AssemblyTreasuryCertificationEventRecord>(entity =>
        {
            entity.ToTable("assembly_treasury_certification_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ActorSubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.OpeningPendingAmount).HasPrecision(18, 2);
            entity.Property(x => x.SourceEvidenceReference).HasMaxLength(500);
            entity.Property(x => x.EvidenceDigestSha256).HasMaxLength(64);
            entity.Property(x => x.ReviewEvidenceReference).HasMaxLength(500);
            entity.Property(x => x.ReviewNote).HasMaxLength(2000);
            entity.HasOne<AssemblyRecord>().WithMany()
                .HasForeignKey(x => x.AssemblyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasAlternateKey(x => new { x.AssemblyId, x.OrganizationId, x.Id });
            entity.HasOne(x => x.ProposalEvent).WithMany()
                .HasForeignKey(x => new { x.AssemblyId, x.OrganizationId, x.ProposalEventId })
                .HasPrincipalKey(x => new { x.AssemblyId, x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.AssemblyId, x.OrganizationId, x.OccurredAtUtc });
            entity.HasIndex(x => new { x.AssemblyId, x.OrganizationId, x.ProposalEventId })
                .IsUnique().HasFilter("\"ProposalEventId\" IS NOT NULL");
        });

        modelBuilder.Entity<AssemblyTreasurySignedReviewRecord>(entity =>
        {
            entity.ToTable("assembly_treasury_signed_reviews");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReviewerSubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.TrustedKeyId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DocumentSha256Hex).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SignatureBase64).HasMaxLength(2048).IsRequired();
            entity.HasOne(x => x.ProposalEvent).WithMany()
                .HasForeignKey(x => new { x.AssemblyId, x.OrganizationId, x.ProposalEventId })
                .HasPrincipalKey(x => new { x.AssemblyId, x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReviewEvent).WithMany()
                .HasForeignKey(x => new { x.AssemblyId, x.OrganizationId, x.ReviewEventId })
                .HasPrincipalKey(x => new { x.AssemblyId, x.OrganizationId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ReviewEventId).IsUnique();
            entity.HasIndex(x => new { x.AssemblyId, x.OrganizationId, x.RecordedAtUtc });
        });

        modelBuilder.Entity<InstitutionalRestrictionRecord>(entity =>
        {
            entity.ToTable("institutional_restrictions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceAuthority).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RestrictionType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => new { x.PersonId, x.StartsOn, x.EndsOn });
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
