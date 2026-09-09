using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Privacy.Entities;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Data;

public sealed class PmgmDbContext(DbContextOptions<PmgmDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MemberTransfer> MemberTransfers => Set<MemberTransfer>();
    public DbSet<InstitutionalStatusEvent> InstitutionalStatusEvents => Set<InstitutionalStatusEvent>();
    public DbSet<DegreeEvent> DegreeEvents => Set<DegreeEvent>();
    public DbSet<OfficeAssignment> OfficeAssignments => Set<OfficeAssignment>();
    public DbSet<FinancialRegularitySnapshot> FinancialRegularitySnapshots => Set<FinancialRegularitySnapshot>();
    public DbSet<HospitalariaRegularitySnapshot> HospitalariaRegularitySnapshots => Set<HospitalariaRegularitySnapshot>();
    public DbSet<CeremonyRequest> CeremonyRequests => Set<CeremonyRequest>();
    public DbSet<CeremonyValidation> CeremonyValidations => Set<CeremonyValidation>();
    public DbSet<CandidatePublication> CandidatePublications => Set<CandidatePublication>();
    public DbSet<InstitutionalRuleSetting> InstitutionalRuleSettings => Set<InstitutionalRuleSetting>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<DataProcessingActivity> DataProcessingActivities => Set<DataProcessingActivity>();
    public DbSet<DataRetentionPolicy> DataRetentionPolicies => Set<DataRetentionPolicy>();
    public DbSet<DataSubjectRequest> DataSubjectRequests => Set<DataSubjectRequest>();
    public DbSet<DataProcessor> DataProcessors => Set<DataProcessor>();
    public DbSet<InternationalDataTransfer> InternationalDataTransfers => Set<InternationalDataTransfer>();
    public DbSet<PrivacySecurityIncident> PrivacySecurityIncidents => Set<PrivacySecurityIncident>();
    public DbSet<PrivacyImpactAssessment> PrivacyImpactAssessments => Set<PrivacyImpactAssessment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("people");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstNames).HasMaxLength(160).IsRequired();
            entity.Property(x => x.LastNames).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(80);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Number).HasMaxLength(40);
            entity.Property(x => x.Type).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(x => x.ParentOrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("members");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InstitutionalNumber).HasMaxLength(80);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Person)
                .WithOne()
                .HasForeignKey<Member>(x => x.PersonId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PersonId).IsUnique();
            entity.HasIndex(x => x.InstitutionalNumber).IsUnique();
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MembershipType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EndReason).HasMaxLength(240);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MemberId, x.OrganizationId, x.StartDate });
        });

        modelBuilder.Entity<MemberTransfer>(entity =>
        {
            entity.ToTable("member_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.Resolution).HasMaxLength(2000);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SourceMembership).WithMany().HasForeignKey(x => x.SourceMembershipId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SourceOrganization).WithMany().HasForeignKey(x => x.SourceOrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TargetOrganization).WithMany().HasForeignKey(x => x.TargetOrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TargetMembership).WithMany().HasForeignKey(x => x.TargetMembershipId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MemberId, x.RequestedDate });
            entity.HasIndex(x => x.SourceMembershipId);
            entity.HasIndex(x => x.TargetMembershipId);
        });

        modelBuilder.Entity<InstitutionalStatusEvent>(entity =>
        {
            entity.ToTable("institutional_status_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MemberId, x.EffectiveDate });
        });

        modelBuilder.Entity<DegreeEvent>(entity =>
        {
            entity.ToTable("degree_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Degree).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MemberId, x.EffectiveDate });
        });

        modelBuilder.Entity<OfficeAssignment>(entity =>
        {
            entity.ToTable("office_assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OfficeType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Period).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MemberId, x.OrganizationId, x.StartDate });
        });

        modelBuilder.Entity<FinancialRegularitySnapshot>(entity =>
        {
            entity.ToTable("financial_regularity_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scope).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.MemberId, x.AsOfDate });
        });

        modelBuilder.Entity<HospitalariaRegularitySnapshot>(entity =>
        {
            entity.ToTable("hospitalaria_regularity_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.AsOfDate });
        });

        modelBuilder.Entity<CeremonyRequest>(entity =>
        {
            entity.ToTable("ceremony_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CeremonyType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CandidatePerson).WithMany().HasForeignKey(x => x.CandidatePersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.CeremonyType, x.Status });
        });

        modelBuilder.Entity<CeremonyValidation>(entity =>
        {
            entity.ToTable("ceremony_validations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ValidationType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.CeremonyRequest).WithMany().HasForeignKey(x => x.CeremonyRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CeremonyRequestId, x.ValidationType, x.RecordedAtUtc });
        });

        modelBuilder.Entity<CandidatePublication>(entity =>
        {
            entity.ToTable("candidate_publications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RuleCode).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SuspensionReason).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.CeremonyRequest).WithMany().HasForeignKey(x => x.CeremonyRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PersonId, x.PublishedFromUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
        });

        modelBuilder.Entity<InstitutionalRuleSetting>(entity =>
        {
            entity.ToTable("institutional_rule_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Code, x.EffectiveFrom });
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ActorSubject).HasMaxLength(320);
            entity.Property(x => x.ActorDisplayName).HasMaxLength(320);
            entity.Property(x => x.Result).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(160).IsRequired();
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.Property(x => x.OccurredAtUtc).IsRequired();
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
            entity.HasIndex(x => x.CorrelationId);
        });

        modelBuilder.Entity<DataRetentionPolicy>(entity =>
        {
            entity.ToTable("data_retention_policies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.DataCategory).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.LegalBasis).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ExpirationEvent).HasMaxLength(160);
            entity.Property(x => x.ExpirationAction).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ApprovedBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<DataSubjectRequest>(entity =>
        {
            entity.ToTable("data_subject_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RequestorReference).HasMaxLength(320).IsRequired();
            entity.Property(x => x.DataCategory).HasMaxLength(160);
            entity.Property(x => x.Purpose).HasMaxLength(1000);
            entity.Property(x => x.Decision).HasMaxLength(1000);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.DueAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Status, x.DueAtUtc });
        });

        modelBuilder.Entity<DataProcessingActivity>(entity =>
        {
            entity.ToTable("data_processing_activities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.LegalBasis).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DataCategories).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.DataSubjects).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Recipients).HasMaxLength(1000);
            entity.Property(x => x.InternationalTransfers).HasMaxLength(1000);
            entity.Property(x => x.RetentionRule).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.SecurityMeasures).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.OwnerRole).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<DataProcessor>(entity =>
        {
            entity.ToTable("data_processors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.DataCategories).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ContractReference).HasMaxLength(500);
            entity.Property(x => x.Country).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<InternationalDataTransfer>(entity =>
        {
            entity.ToTable("international_data_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProcessorName).HasMaxLength(240).IsRequired();
            entity.Property(x => x.DestinationCountry).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TransferMechanism).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DataCategories).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.DestinationCountry, x.Status });
        });

        modelBuilder.Entity<PrivacySecurityIncident>(entity =>
        {
            entity.ToTable("privacy_security_incidents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Severity).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.DataCategories).HasMaxLength(2000);
            entity.Property(x => x.AffectedDataSubjects).HasMaxLength(1000);
            entity.Property(x => x.NotificationReference).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Status, x.Severity });
        });

        modelBuilder.Entity<PrivacyImpactAssessment>(entity =>
        {
            entity.ToTable("privacy_impact_assessments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Scope).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.RiskLevel).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.MitigationPlan).HasMaxLength(4000);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Status, x.RiskLevel });
        });
    }
}
