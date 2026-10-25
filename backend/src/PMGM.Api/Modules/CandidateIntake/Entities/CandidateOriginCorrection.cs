namespace PMGM.Api.Modules.CandidateIntake.Entities;

/// <summary>
/// Historial privado, append-only, de correcciones justificadas del origen.
/// Nunca se publica en listados generales ni en el portal de candidatos.
/// </summary>
public sealed class CandidateOriginCorrection
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CeremonyRequestId { get; set; }
    public string? PreviousKind { get; set; }
    public Guid? PreviousRecommenderMemberId { get; set; }
    public string? PreviousOtherDescription { get; set; }
    public required string NewKind { get; set; }
    public Guid? NewRecommenderMemberId { get; set; }
    public string? NewOtherDescription { get; set; }
    public required string Reason { get; set; }
    public required string ChangedBySubject { get; set; }
    public DateTimeOffset ChangedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
