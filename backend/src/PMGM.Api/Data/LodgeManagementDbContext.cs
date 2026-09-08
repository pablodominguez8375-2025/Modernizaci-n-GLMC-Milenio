using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.LodgeManagement.Entities;

namespace PMGM.Api.Data;

public sealed class LodgeManagementDbContext(DbContextOptions<LodgeManagementDbContext> options) : DbContext(options)
{
    public DbSet<LodgeMeeting> LodgeMeetings => Set<LodgeMeeting>();
    public DbSet<LodgeAttendanceRecord> LodgeAttendanceRecords => Set<LodgeAttendanceRecord>();
    public DbSet<LodgeMinute> LodgeMinutes => Set<LodgeMinute>();
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
