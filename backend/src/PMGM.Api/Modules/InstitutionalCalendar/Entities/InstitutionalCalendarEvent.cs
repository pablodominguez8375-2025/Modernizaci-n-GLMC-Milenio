namespace PMGM.Api.Modules.InstitutionalCalendar.Entities;

public sealed class InstitutionalCalendarEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string EventType { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public string TimeZoneId { get; set; } = "America/Santiago";
    public string? LocationDisplay { get; set; }
    public Guid? SpaceId { get; set; }
    public Guid? OrganizationId { get; set; }
    public required string ScopeType { get; set; }
    public string? ScopeReference { get; set; }
    public required string Visibility { get; set; }
    public required string Status { get; set; }
    public required string SourceModule { get; set; }
    public required string SourceEntityType { get; set; }
    public required string SourceEntityId { get; set; }
    public bool SourceControlled { get; set; }
    public bool OccupancyOnlyWhenRestricted { get; set; } = true;
    public string? ResponsibleSubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
