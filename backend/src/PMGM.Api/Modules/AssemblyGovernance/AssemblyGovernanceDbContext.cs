using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.AssemblyGovernance.Entities;

namespace PMGM.Api.Modules.AssemblyGovernance;

public sealed class AssemblyGovernanceDbContext(DbContextOptions<AssemblyGovernanceDbContext> options) : DbContext(options)
{
    public DbSet<InstitutionalAssembly> Assemblies => Set<InstitutionalAssembly>();
    public DbSet<AssemblyMemberStatus> AssemblyMemberStatuses => Set<AssemblyMemberStatus>();
    public DbSet<AssemblyEligibility> AssemblyEligibilities => Set<AssemblyEligibility>();
    public DbSet<InstitutionalRestriction> InstitutionalRestrictions => Set<InstitutionalRestriction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<InstitutionalAssembly>(entity =>
        {
            entity.ToTable("assemblies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.FrozenBySubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.AssemblyDate, x.Status });
        });

        modelBuilder.Entity<AssemblyMemberStatus>(entity =>
        {
            entity.ToTable("assembly_member_statuses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.PersonId, x.LodgeId, x.Active });
            entity.HasIndex(x => new { x.LodgeId, x.Category, x.Active });
        });

        modelBuilder.Entity<AssemblyEligibility>(entity =>
        {
            entity.ToTable("assembly_eligibilities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.PrimaryReason).HasMaxLength(160);
            entity.Property(x => x.EvaluatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RightsSnapshotReference).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.AssemblyId, x.PersonId }).IsUnique();
            entity.HasIndex(x => new { x.AssemblyId, x.LodgeId, x.Status });
        });

        modelBuilder.Entity<InstitutionalRestriction>(entity =>
        {
            entity.ToTable("institutional_restrictions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Origin).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RestrictionType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Grounds).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.CaseReference).HasMaxLength(500);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.PersonId, x.Active, x.StartDate });
        });
    }
}
