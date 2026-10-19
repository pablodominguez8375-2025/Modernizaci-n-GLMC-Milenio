namespace PMGM.Api.Modules.RegimenInterior.Entities;

/// <summary>
/// Registro inmutable de propuesta/revisión documental GT. Ninguna fila es un
/// certificado financiero habilitante y ninguna contiene el sentido del voto.
/// Propuesta y revisión posterior deben pertenecer a la misma Asamblea/Taller.
/// </summary>
public sealed class AssemblyTreasuryCertificationEventRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Action { get; set; }
    public required string ActorSubject { get; set; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    // Solo para revisión: FK de referencia de propuesta del mismo Taller/Asamblea.
    public Guid? ProposalEventId { get; set; }
    public AssemblyTreasuryCertificationEventRecord? ProposalEvent { get; set; }

    // Solo propuesta, con referencias documentales verificables por otro actor.
    public DateOnly? CoverageFrom { get; set; }
    public DateOnly? CutoffDate { get; set; }
    public string? Currency { get; set; }
    public decimal? OpeningPendingAmount { get; set; }
    public string? SourceEvidenceReference { get; set; }
    public string? EvidenceDigestSha256 { get; set; }

    // Solo revisión: observaciones sustentadas, nunca aprobación de sufragio.
    public string? ReviewEvidenceReference { get; set; }
    public string? ReviewNote { get; set; }
}
