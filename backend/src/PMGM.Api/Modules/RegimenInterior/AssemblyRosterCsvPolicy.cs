using System.Text;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Genera solo VISTAS PRELIMINARES CSV para los dos listados institucionales.
/// La descarga oficial requiere un cierre inmutable y autorización RI/GS en
/// un endpoint futuro; nunca debe darse acceso directo a este generador.
/// </summary>
public static class AssemblyRosterCsvPolicy
{
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
    private const int MaxRows = 10000;

    public static AssemblyRosterCsvPreview BuildPreview(
        IEnumerable<AssemblyRosterCandidate> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var candidates = source.ToArray();
        if (candidates.Length > MaxRows ||
            candidates.Any(x => x.PersonId == Guid.Empty ||
                string.IsNullOrWhiteSpace(x.LastNames) ||
                string.IsNullOrWhiteSpace(x.FirstNames) ||
                string.IsNullOrWhiteSpace(x.Rut) ||
                string.IsNullOrWhiteSpace(x.Workshop) ||
                x.Eligibility is null) ||
            candidates.Select(x => x.PersonId).Distinct().Count() != candidates.Length)
            throw new ArgumentException("INVALID_ROSTER_CANDIDATES", nameof(source));

        // No tomar MayVote=true aislado de un estado contradictorio.
        if (candidates.Any(x => x.Eligibility.MayVote &&
                (x.Eligibility.Status != AssemblyVoterEligibilityPolicy.Eligible ||
                 !x.Eligibility.MayAttend)))
            throw new ArgumentException("INCONSISTENT_PRELIMINARY_ELIGIBILITY", nameof(source));

        var full = AssemblyRosterPolicy.Asambleistas(candidates);
        var suffrage = AssemblyRosterPolicy.PadronSufragantes(candidates);
        return new AssemblyRosterCsvPreview(
            Serialize(full, includeSuffrageMark: true),
            Serialize(suffrage, includeSuffrageMark: false),
            false, // No es la constancia de cierre autenticada por Régimen Interior.
            false);
    }

    /// <summary>CSV de un listado de una Asamblea CERRADA (el llamador ya verificó el cierre y enmascaró el RUT).</summary>
    public static byte[] SerializeOfficial(IReadOnlyCollection<AssemblyRosterPrintRow> rows, bool includeSuffrageMark, string markHeader = "Estado de sufragio")
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > MaxRows) throw new ArgumentException("INVALID_ROSTER_CANDIDATES", nameof(rows));
        return Serialize(rows, includeSuffrageMark, markHeader);
    }

    private static byte[] Serialize(
        IReadOnlyCollection<AssemblyRosterPrintRow> rows, bool includeSuffrageMark, string markHeader = "Estado de sufragio")
    {
        var buffer = new StringBuilder();
        var header = includeSuffrageMark
            ? new[] { "N°", "Apellidos y nombres", "RUT", "Taller", "Firma", markHeader }
            : new[] { "N°", "Apellidos y nombres", "RUT", "Taller", "Firma" };
        AppendLine(buffer, header);

        foreach (var row in rows)
        {
            // No exportar firmas reales. Firma es siempre una casilla en blanco
            // para la planilla impresa. Evita filtraciones desde el llamador.
            var values = new List<string>(6)
            {
                row.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.LastNamesAndFirstNames, row.Rut, row.Workshop, ""
            };
            if (includeSuffrageMark)
                values.Add(row.SuffrageMark switch
                {
                    "Habilitado" => "Habilitado",
                    "Pendiente" => "Pendiente",
                    { Length: > 0 } other when markHeader != "Estado de sufragio" => other,
                    _ => "Sin sufragio"
                });
            AppendLine(buffer, values);
        }

        return [.. Utf8Bom.GetPreamble(), .. Encoding.UTF8.GetBytes(buffer.ToString())];
    }

    private static void AppendLine(StringBuilder output, IEnumerable<string> columns)
    {
        if (output.Length > 0) output.Append("\r\n");
        output.AppendJoin(';', columns.Select(QuoteSafe));
    }

    private static string QuoteSafe(string? input)
    {
        // Ningún salto de línea/control puede crear una fila/columna adicional.
        // No se eliminan acentos ni Ñ; solo controles de transporte.
        var sanitized = new string((input ?? "")
            .Select(c => char.IsControl(c) ? ' ' : c).ToArray());

        // Las planillas pueden tratar como fórmula un texto que empieza con
        // = + - @ incluso tras espacios invisibles o caracteres de formato.
        var firstVisible = sanitized.AsSpan().TrimStart();
        while (!firstVisible.IsEmpty &&
               firstVisible[0] is '\u200B' or '\u2060' or '\uFEFF')
            firstVisible = firstVisible[1..].TrimStart();
        if (!firstVisible.IsEmpty &&
            firstVisible[0] is '=' or '+' or '-' or '@')
            sanitized = "'" + sanitized;

        return "\"" + sanitized.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

public sealed record AssemblyRosterCsvPreview(
    byte[] GeneralAssemblyMembersCsvUtf8,
    byte[] PreliminarySuffragistsCsvUtf8,
    bool MayPublishAsOfficialRoster,
    bool MayAuthorizeSuffrage);
