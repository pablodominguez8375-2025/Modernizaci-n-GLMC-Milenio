using PMGM.Api.Modules.Audit.Entities;

namespace PMGM.Api.Modules.GrandSecretariat.Entities;

public sealed class InstitutionalSpace
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string SpaceType { get; set; }
    public string? Location { get; set; }
    public int? Capacity { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class InstitutionalSpaceReservation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SpaceId { get; set; }
    public InstitutionalSpace Space { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Guid? CeremonyRequestId { get; set; }
    public required string Purpose { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public required string Status { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class SecretariatDocument
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string DocumentType { get; set; }
    public required string DocumentCode { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? RelatedCeremonyRequestId { get; set; }
    public Guid? SpaceReservationId { get; set; }
    public InstitutionalSpaceReservation? SpaceReservation { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public required string IssuedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class GrandSecretariatAuditEvent : AuditEvent
{
}
