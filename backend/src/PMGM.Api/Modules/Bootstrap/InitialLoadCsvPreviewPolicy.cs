using System.Text;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Offline dry-run preview of already-decoded CSV text. The caller must not
/// supply real personal data until a later, separately approved legal/security
/// workflow exists. There is no HTTP endpoint, persistence, logging or apply.
/// The only public result contains canonical metadata, never CSV cell values.
/// </summary>
public static class InitialLoadCsvPreviewPolicy
{
    public sealed record Finding(string Sheet, int Row, string Field, string Code);
    public sealed record Result(
        bool Valid,
        bool CanApply,
        int WritesPerformed,
        IReadOnlyDictionary<string, int> DataRowCounts,
        IReadOnlyList<Finding> Findings);

    public static Result Inspect(IReadOnlyDictionary<string, string>? csvFiles)
    {
        var shape = InitialLoadCsvShapePolicy.Inspect(csvFiles);
        if (!shape.Valid || csvFiles is null)
            return new Result(false, false, 0, shape.DataRowCounts,
                shape.Findings.Select(x => new Finding(x.Sheet, x.Row, x.Field, x.Code)).ToArray());

        var rows = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>(
            StringComparer.Ordinal);
        foreach (var definition in InitialLoadTemplateEndpoints.Sheets)
        {
            if (!TryReadValidatedRows(csvFiles[definition.Name], definition.Columns, out var parsed))
                return new Result(false, false, 0, shape.DataRowCounts,
                    [new Finding(definition.Name, 0, "content", "INVALID_CSV_STRUCTURE")]);
            rows.Add(definition.Name, parsed);
        }

        var preflight = InitialLoadPreflightPolicy.Inspect(rows);
        return new Result(
            preflight.Valid,
            false,
            0,
            shape.DataRowCounts,
            preflight.Findings.Select(x => new Finding(x.Sheet, x.Row, x.Field, x.Code))
                .ToArray());
    }

    /// <summary>
    /// Consumes only CSV already accepted by the bounded shape policy. Cells
    /// exist temporarily in this method and cannot escape in result/errors.
    /// Multiline quoted values are not supported by the shape contract.
    /// </summary>
    private static bool TryReadValidatedRows(
        string csv, string[] columns,
        out IReadOnlyList<IReadOnlyDictionary<string, string>> parsed)
    {
        var items = new List<IReadOnlyDictionary<string, string>>();
        var fields = new List<string>(columns.Length);
        var current = new StringBuilder();
        var quoted = false;
        var rowNumber = 0;
        var complete = true;
        var atStart = csv.Length > 0 && csv[0] == '\uFEFF' ? 1 : 0;

        bool FinishRow()
        {
            if (fields.Count != columns.Length)
                return false;
            if (rowNumber > 0)
            {
                var record = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var index = 0; index < columns.Length; index++)
                    record.Add(columns[index], fields[index]);
                items.Add(record);
                if (items.Count > InitialLoadCsvShapePolicy.MaxRowsPerSheet)
                    return false;
            }
            fields.Clear();
            rowNumber++;
            return true;
        }

        for (var i = atStart; i < csv.Length; i++)
        {
            var c = csv[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else quoted = false;
                }
                else current.Append(c);
                continue;
            }

            if (c == '"' && current.Length == 0)
            {
                quoted = true;
                continue;
            }
            if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    i++;
                fields.Add(current.ToString());
                current.Clear();
                if (!FinishRow())
                {
                    complete = false;
                    break;
                }
                continue;
            }
            current.Append(c);
        }

        if (complete && (fields.Count > 0 || current.Length > 0 ||
            (csv.Length > atStart && csv[^1] == ',')))
        {
            fields.Add(current.ToString());
            complete = FinishRow();
        }

        parsed = complete && !quoted && rowNumber > 0 && items.Count <= InitialLoadCsvShapePolicy.MaxRowsPerSheet
            ? items : Array.Empty<IReadOnlyDictionary<string, string>>();
        return complete && !quoted && rowNumber > 0;
    }
}
