using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.LodgeManagement.Entities;

namespace PMGM.Api.Data;

public sealed class LodgeManagementDbContext(DbContextOptions<LodgeManagementDbContext> options) : DbContext(options)
{
    public DbSet<LodgeMeeting> LodgeMeetings => Set<LodgeMeeting>();
    public DbSet<LodgeAttendanceRecord> LodgeAttendanceRecords => Set<LodgeAttendanceRecord>();
    public DbSet<LodgeMinute> LodgeMinutes => Set<LodgeMinute>();
    public DbSet<LodgeInstructionSession> LodgeInstructionSessions => Set<LodgeInstructionSession>();
    public DbSet<LodgeInstructionAttendanceRecord> LodgeInstructionAttendanceRecords => Set<LodgeInstructionAttendanceRecord>();
    public DbSet<LodgeCorrespondenceRecord> LodgeCorrespondenceRecords => Set<LodgeCorrespondenceRecord>();
    public DbSet<LodgeSecretariatTask> LodgeSecretariatTasks => Set<LodgeSecretariatTask>();
    public DbSet<LodgeMeetingAgendaItem> LodgeMeetingAgendaItems => Set<LodgeMeetingAgendaItem>();
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

        modelBuilder.Entity<LodgeCorrespondenceRecord>(entity =>
        {
            entity.ToTable("lodge_correspondence_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Folio).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Counterparty).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ExternalReference).HasMaxLength(240);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.UpdatedBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Folio }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.CorrespondenceDate });
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
        });

        modelBuilder.Entity<LodgeSecretariatTask>(entity =>
        {
            entity.ToTable("lodge_secretariat_tasks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(2000);
            entity.Property(x => x.Priority).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ResponsibleLabel).HasMaxLength(160);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CompletedBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Status, x.DueDate });
        });

        modelBuilder.Entity<LodgeMeetingAgendaItem>(entity =>
        {
            entity.ToTable("lodge_meeting_agenda_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(3000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.UpdatedBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Meeting).WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MeetingId, x.Position }).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
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
