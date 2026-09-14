namespace PMGM.Api.Modules.Audit.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? ActorSubject { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? IpAddress { get; set; }
    public string? Menu { get; set; }
    public string? Submenu { get; set; }
    public string? Summary { get; set; }
    public required string Result { get; set; }
    public required string CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
}
