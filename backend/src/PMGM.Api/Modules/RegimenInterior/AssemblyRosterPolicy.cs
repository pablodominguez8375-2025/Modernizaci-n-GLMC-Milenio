using System.Globalization;
using System.Text;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Proyecciones de impresión, no padrón cerrado ni derecho a sufragio.
/// Nunca incluye causa privada, resolución disciplinaria ni sentido del voto.
/// </summary>
public static class AssemblyRosterPolicy
{
    private static readonly CompareInfo SpanishChile =
        CultureInfo.GetCultureInfo("es-CL").CompareInfo;

    public static IReadOnlyList<AssemblyRosterPrintRow> Asambleistas(IEnumerable<AssemblyRosterCandidate> candidates) =>
        Order(candidates).Select((candidate, index) => new AssemblyRosterPrintRow(
            index + 1, FormatName(candidate), candidate.Rut,
            candidate.Workshop, "", candidate.Eligibility.MayVote
                ? "Habilitado"
                : candidate.Eligibility.Status == AssemblyVoterEligibilityPolicy.Pending
                    ? "Pendiente"
                    : "Sin sufragio")).ToArray();

    public static IReadOnlyList<AssemblyRosterPrintRow> PadronSufragantes(IEnumerable<AssemblyRosterCandidate> candidates) =>
        Order(candidates.Where(x => x.Eligibility.MayVote &&
                    x.Eligibility.Status == AssemblyVoterEligibilityPolicy.Eligible))
            .Select((candidate, index) => new AssemblyRosterPrintRow(
                index + 1, FormatName(candidate), candidate.Rut, candidate.Workshop, "", null))
            .ToArray();

    internal static IEnumerable<AssemblyRosterCandidate> Order(IEnumerable<AssemblyRosterCandidate> source) =>
        source.OrderBy(x => NormalizeForSpanishCollation(x.LastNames),
                Comparer<string>.Create((a, b) => SpanishChile.Compare(a, b, CompareOptions.IgnoreCase)))
            .ThenBy(x => NormalizeForSpanishCollation(x.FirstNames),
                Comparer<string>.Create((a, b) => SpanishChile.Compare(a, b, CompareOptions.IgnoreCase)))
            .ThenBy(x => x.Rut, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PersonId);

    private static string FormatName(AssemblyRosterCandidate candidate) =>
        string.Concat(candidate.LastNames.Trim(), ", ", candidate.FirstNames.Trim());

    // Ignorar tildes agudas/diéresis, pero conservar el signo de la Ñ:
    // la colación es-CL ubica Ñ después de N y antes de O.
    public static string NormalizeForSpanishCollation(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder();
        char previousBase = '\0';
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                if (c == '\u0303' && (previousBase == 'n' || previousBase == 'N'))
                    normalized.Append(c);
                continue;
            }
            previousBase = c;
            normalized.Append(c);
        }
        return normalized.ToString().Normalize(NormalizationForm.FormC);
    }
}

public sealed record AssemblyRosterCandidate(
    Guid PersonId, string LastNames, string FirstNames, string Rut,
    string Workshop, AssemblyEligibilityDecision Eligibility);

public sealed record AssemblyRosterPrintRow(
    int Number, string LastNamesAndFirstNames, string Rut,
    string Workshop, string Signature, string? SuffrageMark);
