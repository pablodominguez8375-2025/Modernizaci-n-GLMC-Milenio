namespace PMGM.Api.Modules.RegimenInterior.Entities;

/// <summary>
/// Constancia institucional de Régimen Interior para una Asamblea (append-only): 'attest' o 'revoke'.
/// Declara que una fuente (membresía, períodos de Venerable Maestro, Tribunal de Honor, Régimen Interior)
/// quedó verificada y completa AL CORTE, con la referencia al acta que la respalda.
/// No concede sufragio ni cierra el padrón; el sistema no la infiere ni la simula.
/// </summary>
public sealed class AssemblyAttestationEventRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public required string Kind { get; set; }
    public required string Action { get; set; }
    public required string ActaReference { get; set; }
    public required string Statement { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateOnly CutoffDate { get; set; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public Guid? ReversesEventId { get; set; }
}
