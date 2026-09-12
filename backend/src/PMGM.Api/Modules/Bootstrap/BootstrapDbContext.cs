using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Bootstrap.Entities;
using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Bootstrap;

public sealed class BootstrapDbContext(DbContextOptions<BootstrapDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<InstitutionalBootstrapApplication> BootstrapApplications => Set<InstitutionalBootstrapApplication>();
    public DbSet<SecurityProfileDefinition> SecurityProfiles => Set<SecurityProfileDefinition>();
    public DbSet<OfficeDefinition> OfficeDefinitions => Set<OfficeDefinition>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

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

        modelBuilder.Entity<InstitutionalBootstrapApplication>(entity =>
        {
            entity.ToTable("institutional_bootstrap_applications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PackageKey).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PayloadSha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SummaryJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.AppliedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.PackageKey, x.PackageVersion }).IsUnique();
        });

        modelBuilder.Entity<SecurityProfileDefinition>(entity =>
        {
            entity.ToTable("security_profile_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Scope).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<OfficeDefinition>(entity =>
        {
            entity.ToTable("office_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(40).IsRequired();
            entity.Property(x => x.OrganizationType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.AliasesJson).HasColumnType("jsonb");
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
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
