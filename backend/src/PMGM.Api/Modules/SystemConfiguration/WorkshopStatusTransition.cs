namespace PMGM.Api.Modules.SystemConfiguration;

/// <summary>
/// #1035/#1044: historial append-only de bajas y reactivaciones de Talleres, para reconstruir el estado a cualquier fecha de corte
/// (active→retired→active→retired). <see cref="SubjectRef"/> es el id del Taller SIN clave foránea y con un nombre que no
/// calza el criterio de la guardia T1 ni el de «datos asociados»: la tabla no cambia el digest firmado de T1, no cuenta como
/// dato del Taller y sobrevive aunque el Taller cambie de estado. No se actualiza ni se borra (trigger de la migración manual).
/// </summary>
public sealed class WorkshopStatusTransition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    /// <summary>Orden total de las transiciones (identidad de BD).</summary>
    public long Sequence { get; private set; }
    public Guid SubjectRef { get; init; }
    public required string PreviousStatus { get; init; }
    public required string NextStatus { get; init; }
    public DateTimeOffset EffectiveAtUtc { get; init; }
    public string? ActorSubject { get; init; }
    /// <summary>Referencia de correlación a la auditoría (no texto libre).</summary>
    public string? ReasonRef { get; init; }
    /// <summary>«runtime» (baja/reactivación en línea) o «backfill» (carga autorizada con rol migrador).</summary>
    public string Origin { get; init; } = WorkshopTransitionOrigins.Runtime;
    /// <summary>Falso = la fecha efectiva no es confiable: la fila NO participa en series por fecha de corte.</summary>
    public bool DateKnown { get; init; } = true;
    /// <summary>Instante en que la BD registró la fila (reloj del servidor de BD).</summary>
    public DateTimeOffset RecordedAtUtc { get; private set; }
}

public static class WorkshopTransitionOrigins
{
    public const string Runtime = "runtime";
    public const string Backfill = "backfill";
}

/// <summary>Estado de un Taller a una fecha de corte según su historial de transiciones.</summary>
/// <param name="Known">Falso = la historia a esa fecha es desconocida (fail-closed): el Taller no se trata como activo.</param>
public readonly record struct WorkshopStatusAsOf(bool Retired, DateTimeOffset? RetiredAtUtc, bool Known = true);

public static class WorkshopStatusHistory
{
    /// <summary>
    /// Estado a fin del día de corte, FAIL-CLOSED: <c>Known=false</c> significa historia desconocida, nunca «activo». Es desconocida si
    /// (a) el corte es anterior al inicio certificado de captura (<paramref name="baselineUtc"/>; sin baseline certificada solo el
    /// presente es conocido) y no hay transiciones que lo cubran, o (b) existen filas con fecha no confiable (<c>DateKnown=false</c>)
    /// desde cuya fecha el intervalo queda afectado. Esas filas no participan en series ni se les inventa fecha.
    /// </summary>
    public static WorkshopStatusAsOf Resolve(IReadOnlyList<WorkshopStatusTransition> transitions, string currentStatus, DateTimeOffset? currentRetiredAtUtc, DateOnly asOf,
        DateTimeOffset? baselineUtc = null, DateTimeOffset? nowUtc = null)
    {
        var cutoff = new DateTimeOffset(asOf.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var unknownFrom = transitions.Where(x => !x.DateKnown).Select(x => (DateTimeOffset?)x.EffectiveAtUtc).Min();
        var affected = unknownFrom is not null && cutoff > unknownFrom.Value;
        var ordered = transitions.Where(x => x.DateKnown).OrderBy(x => x.EffectiveAtUtc).ThenBy(x => x.Sequence).ToList();
        bool BeforeBaseline() => baselineUtc is null || cutoff <= baselineUtc.Value;
        WorkshopStatusAsOf Result(bool retired, DateTimeOffset? at, bool known) => new(retired, retired ? at : null, known && !affected);

        if (ordered.Count == 0)
        {
            if (currentStatus == WorkshopRegistryCodes.Retired)
                // De baja hoy y sin historial: solo se afirma con fecha de baja actual anterior al corte; si no, historia desconocida.
                return currentRetiredAtUtc is not null && currentRetiredAtUtc.Value < cutoff
                    ? Result(true, currentRetiredAtUtc, true)
                    : new WorkshopStatusAsOf(true, null, false);
            // Activo hoy y sin transiciones: conocido solo para el presente o después de una baseline certificada.
            return Result(false, null, cutoff > now || !BeforeBaseline());
        }
        var last = ordered.LastOrDefault(x => x.EffectiveAtUtc < cutoff);
        if (last is null)
        {
            var startedRetired = ordered[0].PreviousStatus == WorkshopRegistryCodes.Retired;
            // Antes de la primera transición solo se conoce el estado previo a ella si hay baseline certificada que lo respalde.
            return startedRetired ? new WorkshopStatusAsOf(true, null, false) : Result(false, null, !BeforeBaseline());
        }
        var isRetired = last.NextStatus == WorkshopRegistryCodes.Retired;
        return Result(isRetired, last.EffectiveAtUtc, true);
    }
}
