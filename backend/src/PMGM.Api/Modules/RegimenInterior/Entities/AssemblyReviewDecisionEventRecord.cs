namespace PMGM.Api.Modules.RegimenInterior.Entities;

/// <summary>
/// Resolución/gestión preliminar de RI, inmutable y con respaldo.
/// No contiene resultado de voto ni permite habilitar sufragio.
/// </summary>
public sealed class AssemblyReviewDecisionEventRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public Guid MemberStatusId { get; set; }
    public required string Action { get; set; }
    public required string Reason { get; set; }
    public required string EvidenceReference { get; set; }
    public required string DecidedBySubject { get; set; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
