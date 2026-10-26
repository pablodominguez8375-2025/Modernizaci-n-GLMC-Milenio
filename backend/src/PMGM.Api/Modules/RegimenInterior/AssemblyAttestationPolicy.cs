using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>Reglas puras de las constancias de Régimen Interior (sin base de datos).</summary>
public static class AssemblyAttestationPolicy
{
    public const string Attest = "attest", Revoke = "revoke";
    public const string Membership = "membership", MasterPeriods = "master_periods",
        HonorCoverage = "honor_coverage", RegimenCoverage = "regimen_coverage";

    public static readonly string[] Kinds = [Membership, MasterPeriods, HonorCoverage, RegimenCoverage];

    public static string Title(string kind) => kind switch
    {
        Membership => "Calidad de miembro activo de los asambleístas",
        MasterPeriods => "Períodos de Venerable Maestro",
        HonorCoverage => "Cobertura del Tribunal de Honor",
        RegimenCoverage => "Cobertura de Régimen Interior",
        _ => kind
    };

    /// <summary>Constancias vigentes: 'attest' no revertida por un 'revoke'. Gana la más reciente por tipo.</summary>
    public static IReadOnlyDictionary<string, AssemblyAttestationEventRecord> Current(
        IEnumerable<AssemblyAttestationEventRecord> events)
    {
        var all = events.ToArray();
        var reversed = all.Where(e => e.Action == Revoke && e.ReversesEventId is not null)
            .Select(e => e.ReversesEventId!.Value).ToHashSet();
        return all.Where(e => e.Action == Attest && !reversed.Contains(e.Id))
            .GroupBy(e => e.Kind)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.OccurredAtUtc).First());
    }

    /// <summary>Código de rechazo, o null si la solicitud es válida.</summary>
    public static string? Validate(string? kind, string? action, string? acta, string? statement)
    {
        if (kind is null || !Kinds.Contains(kind)) return "INVALID_ATTESTATION_KIND";
        if (action is not (Attest or Revoke)) return "INVALID_ATTESTATION_ACTION";
        var a = acta?.Trim(); var s = statement?.Trim();
        if (string.IsNullOrWhiteSpace(a) || a.Length > 500) return "ACTA_REFERENCE_REQUIRED";
        if (string.IsNullOrWhiteSpace(s) || s.Length is < 10 or > 2000) return "STATEMENT_REQUIRED";
        return null;
    }
}
