using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.GrandSecretariat.Entities;

namespace PMGM.Api.Data;

public sealed class GrandSecretariatDbContext(DbContextOptions<GrandSecretariatDbContext> options) : DbContext(options)
{
    public DbSet<InstitutionalSpace> InstitutionalSpaces => Set<InstitutionalSpace>();
    public DbSet<InstitutionalSpaceReservation> SpaceReservations => Set<InstitutionalSpaceReservation>();
    public DbSet<SecretariatDocument> SecretariatDocuments => Set<SecretariatDocument>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<InstitutionalSpace>(entity =>
        {
            entity.ToTable("institutional_spaces");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.SpaceType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(500);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.SpaceType, x.Status });
        });

        modelBuilder.Entity<InstitutionalSpaceReservation>(entity =>
        {
            entity.ToTable("institutional_space_reservations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Purpose).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.StartsAtUtc).IsRequired();
            entity.Property(x => x.EndsAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.Space)
                .WithMany()
                .HasForeignKey(x => x.SpaceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SpaceId, x.StartsAtUtc, x.EndsAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.StartsAtUtc });
            entity.HasIndex(x => x.CeremonyRequestId);
        });

        modelBuilder.Entity<SecretariatDocument>(entity =>
        {
            entity.ToTable("secretariat_documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DocumentCode).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(8000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.IssuedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.IssuedAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.SpaceReservation)
                .WithMany()
                .HasForeignKey(x => x.SpaceReservationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.DocumentCode).IsUnique();
            entity.HasIndex(x => new { x.DocumentType, x.Status, x.IssuedAtUtc });
            entity.HasIndex(x => new { x.RelatedCeremonyRequestId, x.DocumentType, x.Status });
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
