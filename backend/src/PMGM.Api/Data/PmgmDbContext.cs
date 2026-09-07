using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;

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
    }
}
