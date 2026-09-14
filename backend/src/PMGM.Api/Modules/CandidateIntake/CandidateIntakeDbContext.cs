using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.CandidateIntake.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

public sealed class CandidateIntakeDbContext(DbContextOptions<CandidateIntakeDbContext> options) : DbContext(options)
{
    public DbSet<CandidateIntakeProfile> CandidateIntakeProfiles => Set<CandidateIntakeProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<CandidateIntakeProfile>(entity =>
        {
            entity.ToTable("candidate_intake_profiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PaternalSurname).HasMaxLength(160);
            entity.Property(x => x.MaternalSurname).HasMaxLength(160);
            entity.Property(x => x.RutOrInstitutionalId).HasMaxLength(80);
            entity.Property(x => x.Nationality).HasMaxLength(120);
            entity.Property(x => x.CivilStatus).HasMaxLength(120);
            entity.Property(x => x.Occupation).HasMaxLength(240);
            entity.Property(x => x.EmployerName).HasMaxLength(240);
            entity.Property(x => x.WorkAddress).HasMaxLength(500);
            entity.Property(x => x.WorkPosition).HasMaxLength(240);
            entity.Property(x => x.WorkPhone).HasMaxLength(80);
            entity.Property(x => x.City).HasMaxLength(160);
            entity.Property(x => x.Orient).HasMaxLength(160);
            entity.Property(x => x.ResponsibleSecretaryName).HasMaxLength(240);
            entity.Property(x => x.PresentersJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.InterviewSummary).HasMaxLength(4000);
            entity.Property(x => x.InternalObservations).HasMaxLength(4000);
            entity.Property(x => x.SubmittedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.UpdatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.SubmittedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => x.CeremonyRequestId).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.SubmittedAtUtc });
            entity.HasIndex(x => x.PersonId);
            entity.HasIndex(x => x.PhotoVersionId);
        });
    }
}
