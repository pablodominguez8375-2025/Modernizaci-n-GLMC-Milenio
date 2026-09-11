using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.HonorTribunal.Entities;

namespace PMGM.Api.Modules.HonorTribunal;

public sealed class HonorTribunalDbContext(DbContextOptions<HonorTribunalDbContext> options) : DbContext(options)
{
    public DbSet<HonorCase> HonorCases => Set<HonorCase>();
    public DbSet<HonorSanction> HonorSanctions => Set<HonorSanction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("restricted");

        modelBuilder.Entity<HonorCase>(entity =>
        {
            entity.ToTable("honor_cases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CaseNumber).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Classification).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Resolution).HasMaxLength(8000);
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.CaseNumber).IsUnique();
            entity.HasIndex(x => new { x.PersonId, x.Status });
        });

        modelBuilder.Entity<HonorSanction>(entity =>
        {
            entity.ToTable("honor_sanctions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasOne(x => x.HonorCase)
                .WithMany()
                .HasForeignKey(x => x.HonorCaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PersonId, x.Active, x.StartsOn });
            entity.HasIndex(x => new { x.HonorCaseId, x.Active });
        });
    }
}
