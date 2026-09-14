using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Notifications.Entities;

namespace PMGM.Api.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("notification_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.SubjectTemplate).HasMaxLength(500);
            entity.Property(x => x.BodyTemplate).HasColumnType("text").IsRequired();
            entity.Property(x => x.AllowedVariablesJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.Sensitivity).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.EffectiveFromUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Code, x.Version }).IsUnique();
        });

        modelBuilder.Entity<NotificationMessage>(entity =>
        {
            entity.ToTable("notification_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TypeCode).HasMaxLength(160).IsRequired();
            entity.Property(x => x.RecipientSubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecipientEmail).HasMaxLength(320);
            entity.Property(x => x.Subject).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Body).HasColumnType("text").IsRequired();
            entity.Property(x => x.ActionUrl).HasMaxLength(1000);
            entity.Property(x => x.Classification).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ActionStatus).HasMaxLength(32);
            entity.Property(x => x.RelatedResourceType).HasMaxLength(80);
            entity.Property(x => x.RelatedResourceId).HasMaxLength(120);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(500).IsRequired();
            entity.Property(x => x.SourceEventId).HasMaxLength(320);
            entity.Property(x => x.CorrelationId).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.NotificationTemplate).WithMany().HasForeignKey(x => x.NotificationTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.RecipientSubject, x.CreatedAtUtc });
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.ToTable("notification_deliveries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Channel).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(120);
            entity.Property(x => x.LastErrorCode).HasMaxLength(160);
            entity.Property(x => x.ScheduledAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.NotificationMessage).WithMany(x => x.Deliveries).HasForeignKey(x => x.NotificationMessageId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
            entity.HasIndex(x => new { x.NotificationMessageId, x.Channel }).IsUnique();
        });

        modelBuilder.Entity<NotificationDeliveryAttempt>(entity =>
        {
            entity.ToTable("notification_delivery_attempts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(120);
            entity.Property(x => x.ErrorCode).HasMaxLength(160);
            entity.Property(x => x.AttemptedAtUtc).IsRequired();
            entity.HasOne(x => x.NotificationDelivery).WithMany(x => x.Attempts).HasForeignKey(x => x.NotificationDeliveryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.NotificationDeliveryId, x.AttemptNumber }).IsUnique();
        });
    }
}
