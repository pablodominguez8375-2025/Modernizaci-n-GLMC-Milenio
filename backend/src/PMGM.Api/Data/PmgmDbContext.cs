using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Privacy.Entities;
using PMGM.Api.Modules.SecretariatOperations.Entities;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Data;

public sealed class PmgmDbContext(DbContextOptions<PmgmDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MemberTransfer> MemberTransfers => Set<MemberTransfer>();
    public DbSet<MemberWithdrawalRequest> MemberWithdrawalRequests => Set<MemberWithdrawalRequest>();
    public DbSet<InstitutionalStatusEvent> InstitutionalStatusEvents => Set<InstitutionalStatusEvent>();
    public DbSet<DegreeEvent> DegreeEvents => Set<DegreeEvent>();
    public DbSet<OfficeAssignment> OfficeAssignments => Set<OfficeAssignment>();
    public DbSet<HistoricalMemberIntake> HistoricalMemberIntakes => Set<HistoricalMemberIntake>();
    public DbSet<HistoricalMemberIntakeOffice> HistoricalMemberIntakeOffices => Set<HistoricalMemberIntakeOffice>();
    public DbSet<LodgeAdministrativeMeeting> LodgeAdministrativeMeetings => Set<LodgeAdministrativeMeeting>();
    public DbSet<LodgeSecretariatRecord> LodgeSecretariatRecords => Set<LodgeSecretariatRecord>();
    public DbSet<LodgeCorrespondence> LodgeCorrespondence => Set<LodgeCorrespondence>();
    public DbSet<LodgeSecretariatTask> LodgeSecretariatTasks => Set<LodgeSecretariatTask>();
    public DbSet<LodgeAgendaItem> LodgeAgendaItems => Set<LodgeAgendaItem>();
    public DbSet<FinancialRegularitySnapshot> FinancialRegularitySnapshots => Set<FinancialRegularitySnapshot>();
    public DbSet<TreasuryMonthlyStatement> TreasuryMonthlyStatements => Set<TreasuryMonthlyStatement>();
    public DbSet<TreasuryMonthlyStatementLine> TreasuryMonthlyStatementLines => Set<TreasuryMonthlyStatementLine>();
    public DbSet<TreasuryPayment> TreasuryPayments => Set<TreasuryPayment>();
    public DbSet<TreasuryAdjustment> TreasuryAdjustments => Set<TreasuryAdjustment>();
    public DbSet<LodgeFeePlan> LodgeFeePlans => Set<LodgeFeePlan>();
    public DbSet<LodgeMemberCharge> LodgeMemberCharges => Set<LodgeMemberCharge>();
    public DbSet<LodgeMemberPayment> LodgeMemberPayments => Set<LodgeMemberPayment>();
    public DbSet<LodgeHospitalariaMovement> LodgeHospitalariaMovements => Set<LodgeHospitalariaMovement>();
    public DbSet<LodgeTreasuryExpense> LodgeTreasuryExpenses => Set<LodgeTreasuryExpense>();
    public DbSet<HospitalariaRegularitySnapshot> HospitalariaRegularitySnapshots => Set<HospitalariaRegularitySnapshot>();
    public DbSet<HospitalariaMonthlySubmission> HospitalariaMonthlySubmissions => Set<HospitalariaMonthlySubmission>();
    public DbSet<HospitalariaReplenishmentRate> HospitalariaReplenishmentRates => Set<HospitalariaReplenishmentRate>();
    public DbSet<DeathReplenishmentCase> DeathReplenishmentCases => Set<DeathReplenishmentCase>();
    public DbSet<DeathReplenishmentObligation> DeathReplenishmentObligations => Set<DeathReplenishmentObligation>();
    public DbSet<DeathReplenishmentPayment> DeathReplenishmentPayments => Set<DeathReplenishmentPayment>();
    public DbSet<DeathReplenishmentTransfer> DeathReplenishmentTransfers => Set<DeathReplenishmentTransfer>();
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
            entity.Property(x => x.Rut).HasMaxLength(16);
            entity.Property(x => x.LastNames).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(80);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Rut).IsUnique();
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
            entity.Property(x => x.CurrentDegree).HasMaxLength(40);
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

        modelBuilder.Entity<MemberWithdrawalRequest>(entity =>
        {
            entity.ToTable("member_withdrawal_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WithdrawalType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Resolution).HasMaxLength(2000);
            entity.Property(x => x.RequestedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.DecidedBySubject).HasMaxLength(320);
            entity.Property(x => x.OratorSignatureSubject).HasMaxLength(320);
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OriginOrganization).WithMany().HasForeignKey(x => x.OriginOrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MemberId, x.Status });
            entity.HasIndex(x => new { x.OriginOrganizationId, x.CreatedAtUtc });
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

        modelBuilder.Entity<HistoricalMemberIntake>(entity =>
        {
            entity.ToTable("historical_member_intakes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstNames).HasMaxLength(160).IsRequired();
            entity.Property(x => x.LastNames).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Rut).HasMaxLength(16);
            entity.Property(x => x.InstitutionalNumber).HasMaxLength(80);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.Phone).HasMaxLength(80);
            entity.Property(x => x.CurrentDegree).HasMaxLength(40).IsRequired();
            entity.Property(x => x.EvidenceReference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ReviewedBySubject).HasMaxLength(320);
            entity.Property(x => x.ReviewNotes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => x.TargetMemberId);
            entity.HasIndex(x => x.Rut);
            entity.HasIndex(x => x.InstitutionalNumber);
        });

        modelBuilder.Entity<HistoricalMemberIntakeOffice>(entity =>
        {
            entity.ToTable("historical_member_intake_offices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OfficeType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Period).HasMaxLength(80).IsRequired();
            entity.HasOne(x => x.Intake).WithMany(x => x.Offices).HasForeignKey(x => x.IntakeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.IntakeId, x.OfficeType, x.Period });
        });

        modelBuilder.Entity<LodgeAdministrativeMeeting>(entity =>
        {
            entity.ToTable("lodge_administrative_meetings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.MeetingDate });
        });

        modelBuilder.Entity<LodgeSecretariatRecord>(entity =>
        {
            entity.ToTable("lodge_secretariat_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RecordType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.SubmittedBySubject).HasMaxLength(320);
            entity.Property(x => x.ReviewedBySubject).HasMaxLength(320);
            entity.Property(x => x.ReviewNotes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.OrganizationId, x.RecordType, x.EventDate });
            entity.HasIndex(x => new { x.RecordType, x.SourceRecordId }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.SubmittedAtUtc });
            entity.HasIndex(x => x.CeremonyAuthorizationDocumentId);
        });

        modelBuilder.Entity<LodgeCorrespondence>(entity =>
        {
            entity.ToTable("lodge_correspondence"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Folio).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Counterparty).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(500);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ClosedBySubject).HasMaxLength(320);
            entity.HasIndex(x => new { x.OrganizationId, x.Folio }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.CorrespondenceDate });
        });

        modelBuilder.Entity<LodgeSecretariatTask>(entity =>
        {
            entity.ToTable("lodge_secretariat_tasks"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(2000);
            entity.Property(x => x.Priority).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Responsible).HasMaxLength(300);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CompletedBySubject).HasMaxLength(320);
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.DueDate });
        });

        modelBuilder.Entity<LodgeAgendaItem>(entity =>
        {
            entity.ToTable("lodge_agenda_items"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.UpdatedBySubject).HasMaxLength(320);
            entity.HasIndex(x => new { x.OrganizationId, x.MeetingId, x.Order }).IsUnique();
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

        modelBuilder.Entity<TreasuryMonthlyStatement>(entity =>
        {
            entity.ToTable("treasury_monthly_statements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RectifiesStatement).WithMany().HasForeignKey(x => x.RectifiesStatementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.PeriodYear, x.PeriodMonth })
                .IsUnique()
                .HasFilter("\"RectifiesStatementId\" IS NULL");
        });

        modelBuilder.Entity<TreasuryMonthlyStatementLine>(entity =>
        {
            entity.ToTable("treasury_monthly_statement_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DegreeCodeAtCutoff).HasMaxLength(80).IsRequired();
            entity.Property(x => x.OfficeCodeAtCutoff).HasMaxLength(120);
            entity.Property(x => x.BaseAmount).HasPrecision(18, 2);
            entity.Property(x => x.AdjustmentAmount).HasPrecision(18, 2);
            entity.Property(x => x.AdjustmentType).HasMaxLength(80);
            entity.Property(x => x.AuthorizationReference).HasMaxLength(500);
            entity.Property(x => x.Observation).HasMaxLength(2000);
            entity.Property(x => x.IdentityMatchStatus).HasMaxLength(40).IsRequired();
            entity.Ignore(x => x.PayableAmount);
            entity.HasOne(x => x.Statement).WithMany(x => x.Lines).HasForeignKey(x => x.StatementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.StatementId, x.MemberId });
        });

        modelBuilder.Entity<TreasuryPayment>(entity =>
        {
            entity.ToTable("treasury_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PaymentMethod).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.SubmissionNumber).IsRequired();
            entity.Property(x => x.PayerDisplayName).HasMaxLength(320).IsRequired();
            entity.Property(x => x.PayerRut).HasMaxLength(40);
            entity.Property(x => x.Reference).HasMaxLength(500);
            entity.HasOne(x => x.Statement).WithMany(x => x.Payments).HasForeignKey(x => x.StatementId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TreasuryAdjustment>(entity =>
        {
            entity.ToTable("treasury_adjustments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AdjustmentType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.AuthorizationReference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MemberId, x.OrganizationId, x.EffectiveFrom });
        });

        modelBuilder.Entity<LodgeFeePlan>(entity =>
        {
            entity.ToTable("lodge_fee_plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FeeType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.MemberAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrandTreasuryAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.FeeType, x.EffectiveFrom }).IsUnique();
        });

        modelBuilder.Entity<LodgeMemberCharge>(entity =>
        {
            entity.ToTable("lodge_member_charges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MemberAmount).HasPrecision(18, 2);
            entity.Property(x => x.GrandTreasuryAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FeePlan).WithMany().HasForeignKey(x => x.FeePlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.MemberId, x.PeriodYear, x.PeriodMonth }).IsUnique();
        });

        modelBuilder.Entity<LodgeMemberPayment>(entity =>
        {
            entity.ToTable("lodge_member_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReceiptNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(500);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.HasOne(x => x.Charge).WithMany(x => x.Payments).HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.ReceiptNumber).IsUnique();
        });

        modelBuilder.Entity<LodgeHospitalariaMovement>(entity =>
        {
            entity.ToTable("lodge_hospitalaria_movements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MovementType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.MemberReference).HasMaxLength(160);
            entity.Property(x => x.Destination).HasMaxLength(240);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.Observation).HasMaxLength(2000);
            entity.Property(x => x.ApprovalStatus).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ApprovalSource).HasMaxLength(30);
            entity.Property(x => x.ApprovedBySubject).HasMaxLength(320);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.MovementDate });
        });

        modelBuilder.Entity<LodgeTreasuryExpense>(entity =>
        {
            entity.ToTable("lodge_treasury_expenses"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired(); entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.ApprovalStatus).HasMaxLength(30).IsRequired(); entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500); entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired(); entity.Property(x => x.ApprovedBySubject).HasMaxLength(320);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.ExpenseDate });
        });

        modelBuilder.Entity<HospitalariaMonthlySubmission>(entity =>
        {
            entity.ToTable("hospitalaria_monthly_submissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IncomeAmount).HasPrecision(18, 2);
            entity.Property(x => x.ApprovedExpenseAmount).HasPrecision(18, 2);
            entity.Property(x => x.PeriodNetAmount).HasPrecision(18, 2);
            entity.Property(x => x.ReplenishmentDueAmount).HasPrecision(18, 2);
            entity.Property(x => x.ReplenishmentPaidAmount).HasPrecision(18, 2);
            entity.Property(x => x.DifferenceAmount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentReference).HasMaxLength(500);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ReviewedBySubject).HasMaxLength(320);
            entity.Property(x => x.ReviewNotes).HasMaxLength(2000);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizationId, x.PeriodYear, x.PeriodMonth }).IsUnique();
            entity.HasIndex(x => new { x.Status, x.SubmittedAtUtc });
        });

        modelBuilder.Entity<HospitalariaReplenishmentRate>(entity =>
        {
            entity.ToTable("hospitalaria_replenishment_rates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AmountPerActiveMember).HasPrecision(18, 2);
            entity.Property(x => x.SourceReference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => x.EffectiveFrom).IsUnique();
        });

        modelBuilder.Entity<DeathReplenishmentCase>(entity =>
        {
            entity.ToTable("hospitalaria_death_replenishment_cases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AmountPerActiveMember).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.HasOne(x => x.DeceasedMember).WithMany().HasForeignKey(x => x.DeceasedMemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.DeathStatusEventId).IsUnique();
            entity.HasIndex(x => x.DeathDate);
        });

        modelBuilder.Entity<DeathReplenishmentObligation>(entity =>
        {
            entity.ToTable("hospitalaria_death_replenishment_obligations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AmountDue).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasOne(x => x.Case).WithMany(x => x.Obligations).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Membership).WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CaseId, x.MembershipId }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
        });

        modelBuilder.Entity<DeathReplenishmentPayment>(entity =>
        {
            entity.ToTable("hospitalaria_death_replenishment_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReceiptNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.HasOne(x => x.Obligation).WithMany(x => x.Payments).HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.ReceiptNumber).IsUnique();
        });

        modelBuilder.Entity<DeathReplenishmentTransfer>(entity =>
        {
            entity.ToTable("hospitalaria_death_replenishment_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.SubmissionNumber).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ReviewedBySubject).HasMaxLength(320);
            entity.Property(x => x.ReviewNotes).HasMaxLength(2000);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CaseId, x.OrganizationId, x.SubmissionNumber }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
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
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.Menu).HasMaxLength(100);
            entity.Property(x => x.Submenu).HasMaxLength(100);
            entity.Property(x => x.Summary).HasMaxLength(300);
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
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Code, x.EffectiveFrom }).IsUnique();
        });

        modelBuilder.Entity<DataSubjectRequest>(entity =>
        {
            entity.ToTable("data_subject_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ResponsibleSubject).HasMaxLength(320);
            entity.Property(x => x.Resolution).HasMaxLength(4000);
            entity.Property(x => x.Grounds).HasMaxLength(4000);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.PersonId);
            entity.HasIndex(x => new { x.Status, x.DueDate });
        });

        modelBuilder.Entity<DataProcessingActivity>(entity =>
        {
            entity.ToTable("data_processing_activities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Module).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.LawfulBasis).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DataCategoriesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.SubjectCategoriesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.RecipientsJson).HasColumnType("jsonb");
            entity.Property(x => x.InternalOwner).HasMaxLength(240);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.RetentionPolicy).WithMany().HasForeignKey(x => x.RetentionPolicyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.RetentionPolicyId);
            entity.HasIndex(x => new { x.Code, x.EffectiveFrom }).IsUnique();
        });

        modelBuilder.Entity<DataProcessor>(entity =>
        {
            entity.ToTable("data_processors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Service).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.CountriesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.DataCategoriesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.SubjectCategoriesJson).HasColumnType("jsonb");
            entity.Property(x => x.SubprocessorsJson).HasColumnType("jsonb");
            entity.Property(x => x.AgreementReference).HasMaxLength(500);
            entity.Property(x => x.TransferMechanism).HasMaxLength(500);
            entity.Property(x => x.IncidentObligations).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Name, x.Service });
        });

        modelBuilder.Entity<InternationalDataTransfer>(entity =>
        {
            entity.ToTable("international_data_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DestinationCountry).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
            entity.Property(x => x.LegalMechanism).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Safeguards).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.DataProcessingActivity).WithMany().HasForeignKey(x => x.DataProcessingActivityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DataProcessor).WithMany().HasForeignKey(x => x.DataProcessorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.DataProcessorId);
            entity.HasIndex(x => new { x.DataProcessingActivityId, x.DestinationCountry, x.EffectiveFrom });
        });

        modelBuilder.Entity<PrivacySecurityIncident>(entity =>
        {
            entity.ToTable("privacy_security_incidents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Source).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Nature).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.DataCategoriesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.RiskLevel).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ImmediateMeasures).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AssessmentSummary).HasMaxLength(4000);
            entity.Property(x => x.AuthorityDecision).HasMaxLength(40);
            entity.Property(x => x.AuthorityDecisionReason).HasMaxLength(4000);
            entity.Property(x => x.AuthorityNotificationChannel).HasMaxLength(120);
            entity.Property(x => x.AuthorityReference).HasMaxLength(500);
            entity.Property(x => x.SubjectsDecision).HasMaxLength(40);
            entity.Property(x => x.SubjectsDecisionReason).HasMaxLength(4000);
            entity.Property(x => x.SubjectsNotificationChannel).HasMaxLength(120);
            entity.Property(x => x.SubjectsNotificationReference).HasMaxLength(500);
            entity.Property(x => x.CorrectiveActions).HasMaxLength(4000);
            entity.Property(x => x.ClosedBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Status, x.DetectedAtUtc });
        });

        modelBuilder.Entity<PrivacyImpactAssessment>(entity =>
        {
            entity.ToTable("privacy_impact_assessments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RiskLevel).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AssessmentSummary).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Mitigations).HasMaxLength(4000);
            entity.Property(x => x.ResidualRisk).HasMaxLength(2000);
            entity.Property(x => x.ApprovedBySubject).HasMaxLength(320);
            entity.Property(x => x.EvidenceReference).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.DataProcessingActivity).WithMany().HasForeignKey(x => x.DataProcessingActivityId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.DataProcessingActivityId, x.CreatedAtUtc });
        });
    }
}
