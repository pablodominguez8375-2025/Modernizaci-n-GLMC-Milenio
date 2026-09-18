using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.LodgeManagement.Entities;

namespace PMGM.Api.Data;

public sealed class LodgeManagementDbContext(DbContextOptions<LodgeManagementDbContext> options) : DbContext(options)
{
    public DbSet<LodgeMeeting> LodgeMeetings => Set<LodgeMeeting>();
    public DbSet<LodgeAttendanceRecord> LodgeAttendanceRecords => Set<LodgeAttendanceRecord>();
    public DbSet<LodgeMinute> LodgeMinutes => Set<LodgeMinute>();
    public DbSet<LodgeAnonymousBallot> LodgeAnonymousBallots => Set<LodgeAnonymousBallot>();
    public DbSet<LodgeInstructionSession> LodgeInstructionSessions => Set<LodgeInstructionSession>();
    public DbSet<LodgeInstructionAttendanceRecord> LodgeInstructionAttendanceRecords => Set<LodgeInstructionAttendanceRecord>();
    public DbSet<LodgeCouncilSession> LodgeCouncilSessions => Set<LodgeCouncilSession>();
    public DbSet<LodgeCouncilAttendanceRecord> LodgeCouncilAttendanceRecords => Set<LodgeCouncilAttendanceRecord>();
    public DbSet<LodgeCouncilDecision> LodgeCouncilDecisions => Set<LodgeCouncilDecision>();
    public DbSet<LodgeCouncilFinancialReview> LodgeCouncilFinancialReviews => Set<LodgeCouncilFinancialReview>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<LodgeMeeting>(entity =>
        {
            entity.ToTable("lodge_meetings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MeetingType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Grade).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CeremonyType).HasMaxLength(40);
            entity.Property(x => x.Modality).HasMaxLength(40).IsRequired();
            entity.Property(x => x.LocationReference).HasMaxLength(500);
            entity.Property(x => x.VirtualAccessReference).HasMaxLength(1000);
            entity.Property(x => x.Title).HasMaxLength(300);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.MeetingDate });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
        });

        modelBuilder.Entity<LodgeAttendanceRecord>(entity =>
        {
            entity.ToTable("lodge_attendance_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ExcuseReason).HasMaxLength(1000);
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Meeting).WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MeetingId, x.MemberId, x.RecordedAtUtc });
        });

        modelBuilder.Entity<LodgeMinute>(entity =>
        {
            entity.ToTable("lodge_minutes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasMaxLength(20000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ApprovedBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Meeting).WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MeetingId, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.MeetingId, x.Status });
        });

        modelBuilder.Entity<LodgeAnonymousBallot>(entity =>
        {
            entity.ToTable("lodge_anonymous_ballots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BallotType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(500).IsRequired();
            entity.Property(x => x.RecountObservation).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Meeting).WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MeetingId, x.Subject, x.ProcedureNumber, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.MeetingId, x.Status });
        });

        modelBuilder.Entity<LodgeInstructionSession>(entity =>
        {
            entity.ToTable("lodge_instruction_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Grade).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Topic).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ResponsibleOffice).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.InstructionDate });
            entity.HasIndex(x => new { x.OrganizationId, x.Grade, x.InstructionDate });
        });

        modelBuilder.Entity<LodgeInstructionAttendanceRecord>(entity =>
        {
            entity.ToTable("lodge_instruction_attendance_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.InstructionSession).WithMany().HasForeignKey(x => x.InstructionSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.InstructionSessionId, x.MemberId, x.RecordedAtUtc });
            entity.HasIndex(x => new { x.MemberId, x.RecordedAtUtc });
        });

        modelBuilder.Entity<LodgeCouncilSession>(entity =>
        {
            entity.ToTable("lodge_council_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.QuorumConfirmedBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.SessionDate });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
        });

        modelBuilder.Entity<LodgeCouncilAttendanceRecord>(entity =>
        {
            entity.ToTable("lodge_council_attendance_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(320).IsRequired();
            entity.Property(x => x.InstitutionalRole).HasMaxLength(80);
            entity.Property(x => x.ParticipationType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SessionId, x.MemberId, x.RecordedAtUtc });
        });

        modelBuilder.Entity<LodgeCouncilDecision>(entity =>
        {
            entity.ToTable("lodge_council_decisions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Resolution).HasMaxLength(5000).IsRequired();
            entity.Property(x => x.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ChamberReference).HasMaxLength(500);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SessionId, x.RecordedAtUtc });
            entity.HasIndex(x => new { x.SessionId, x.Category });
        });

        modelBuilder.Entity<LodgeCouncilFinancialReview>(entity =>
        {
            entity.ToTable("lodge_council_financial_reviews");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ControlArea).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PeriodLabel).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Conclusion).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Observations).HasMaxLength(5000);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SessionId, x.ControlArea });
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
        });
    }
}
