namespace PMGM.Api.Modules.GrandSecretariat.Entities;

/// <summary>Petición de Taller; nunca constituye una reserva hasta aprobación de Gran Secretaría.</summary>
public sealed class InstitutionalSpaceRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string PreferredSpaceType { get; set; }
    public Guid? PreferredSpaceId { get; set; }
    public Guid? CeremonyRequestId { get; set; }
    public required string Purpose { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public string? Notes { get; set; }
    public int? ExpectedAttendees { get; set; }
    public string? EquipmentRequest { get; set; }
    public string? EquipmentResponsible { get; set; }
    public string? VmApprovalReference { get; set; }
    public required string Status { get; set; }
    public string? ResponseReason { get; set; }
    public Guid? SpaceReservationId { get; set; }
    public required string CreatedBySubject { get; set; }
    public string? DecidedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAtUtc { get; set; }
}
