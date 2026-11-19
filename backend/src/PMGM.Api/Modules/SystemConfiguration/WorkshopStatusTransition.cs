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
}

/// <summary>Estado de un Taller a una fecha de corte según su historial de transiciones.</summary>
public readonly record struct WorkshopStatusAsOf(bool Retired, DateTimeOffset? RetiredAtUtc);

public static class WorkshopStatusHistory
{
    /// <summary>
    /// Estado a fin del día de corte. Con transiciones: la última con efecto ≤ ese instante; sin ninguna aún, el estado previo a la
    /// primera. Sin historial (Talleres dados de baja antes de existir la tabla): se usa la fotografía actual (baja con su fecha
    /// si la tiene; sin fecha = de baja), sin inventar intervalos.
    /// </summary>
    public static WorkshopStatusAsOf Resolve(IReadOnlyList<WorkshopStatusTransition> transitions, string currentStatus, DateTimeOffset? currentRetiredAtUtc, DateOnly asOf)
    {
        var cutoff = new DateTimeOffset(asOf.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        if (transitions.Count == 0)
        {
            var retired = currentStatus == WorkshopRegistryCodes.Retired && (currentRetiredAtUtc is null || currentRetiredAtUtc.Value < cutoff);
            return new WorkshopStatusAsOf(retired, retired ? currentRetiredAtUtc : null);
        }
        var ordered = transitions.OrderBy(x => x.Sequence).ToList();
        var last = ordered.LastOrDefault(x => x.EffectiveAtUtc < cutoff);
        if (last is null) return new WorkshopStatusAsOf(ordered[0].PreviousStatus == WorkshopRegistryCodes.Retired, null);
        var isRetired = last.NextStatus == WorkshopRegistryCodes.Retired;
        return new WorkshopStatusAsOf(isRetired, isRetired ? last.EffectiveAtUtc : null);
    }
}
