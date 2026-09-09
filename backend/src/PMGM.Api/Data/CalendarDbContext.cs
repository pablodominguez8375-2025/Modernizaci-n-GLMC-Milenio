using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;

namespace PMGM.Api.Data;

public sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public DbSet<InstitutionalCalendarEvent> CalendarEvents => Set<InstitutionalCalendarEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<InstitutionalCalendarEvent>(entity =>
        {
            entity.ToTable("calendar_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.StartsAtUtc).IsRequired();
            entity.Property(x => x.EndsAtUtc).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LocationDisplay).HasMaxLength(500);
            entity.Property(x => x.ScopeType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ScopeReference).HasMaxLength(320);
            entity.Property(x => x.Visibility).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceModule).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceEntityType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.SourceEntityId).HasMaxLength(320).IsRequired();
            entity.Property(x => x.ResponsibleSubject).HasMaxLength(320);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.StartsAtUtc, x.EndsAtUtc });
            entity.HasIndex(x => new { x.OrganizationId, x.StartsAtUtc });
            entity.HasIndex(x => new { x.SourceModule, x.SourceEntityType, x.SourceEntityId }).IsUnique();
            entity.HasIndex(x => new { x.SpaceId, x.StartsAtUtc });
        });
    }
}
