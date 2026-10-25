using System.Text;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Offline CSV framing check only. Never stores file contents or returns any
/// supplied cell values. Not connected to HTTP, databases or import routines.
/// </summary>
public static class InitialLoadCsvShapePolicy
{
    public const int MaxCharactersPerFile = 800_000;
    public const int MaxRowsPerSheet = 2_000;
    public const int MaxCharactersPerCell = 256;
    public const int MaxFindings = 50;

    public sealed record Finding(string Sheet, int Row, string Field, string Code);
    public sealed record Result(
        bool Valid, bool CanApply, int WritesPerformed,
        IReadOnlyDictionary<string, int> DataRowCounts,
        IReadOnlyList<Finding> Findings);

    public static Result Inspect(IReadOnlyDictionary<string, string>? csvFiles)
    {
        var problems = new List<Finding>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = InitialLoadTemplateEndpoints.Sheets;
        var allowed = files.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

        void Add(string file, int row, string column, string code)
        {
            if (problems.Count < MaxFindings)
                problems.Add(new Finding(file, row, column, code));
        }

        if (csvFiles is null || csvFiles.Count != allowed.Count ||
            !csvFiles.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(allowed))
        {
            Add("schema", 0, "files", "INVALID_SHEETS");
            return new Result(false, false, 0, counts, problems);
        }

        foreach (var definition in files)
        {
            var text = csvFiles[definition.Name];
            if (text is null || text.Length > MaxCharactersPerFile)
            {
                Add(definition.Name, 0, "content", "INVALID_FILE_SIZE");
                continue;
            }

            counts[definition.Name] = InspectOne(
                text, definition.Name, definition.Columns, Add);
        }

        return new Result(problems.Count == 0, false, 0, counts, problems);
    }

    private static int InspectOne(
        string csv, string sheet, string[] expected,
        Action<string, int, string, string> add)
    {
        var cells = new List<string>(expected.Length);
        var cell = new StringBuilder();
        var row = 1;
        var dataRows = 0;
        var quoted = false;
        var closedQuote = false;
        var failed = false;
        var atStart = csv.Length > 0 && csv[0] == '\uFEFF' ? 1 : 0;

        string CurrentField()
            => row == 1 ? "header"
                : cells.Count < expected.Length ? expected[cells.Count] : "columns";

        void Issue(string code)
        {
            add(sheet, row, CurrentField(), code);
            failed = true;
        }

        void EndCell()
        {
            cells.Add(cell.ToString());
            cell.Clear();
            closedQuote = false;
            if (cells.Count > expected.Length)
                Issue("COLUMN_COUNT");
        }

        void EndRow()
        {
            if (row == 1)
            {
                if (!cells.SequenceEqual(expected, StringComparer.Ordinal))
                {
                    add(sheet, 1, "headers", "HEADER_MISMATCH");
                    failed = true;
                }
            }
            else
            {
                dataRows++;
                if (cells.Count != expected.Length)
                    add(sheet, row, "columns", "COLUMN_COUNT");
                if (dataRows > MaxRowsPerSheet)
                {
                    add(sheet, row, "rows", "TOO_MANY_ROWS");
                    failed = true;
                }
            }
            cells.Clear();
            row++;
        }

        for (var i = atStart; i < csv.Length && !failed; i++)
        {
            var c = csv[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                        closedQuote = true;
                    }
                }
                else if (c is '\r' or '\n')
                    Issue("MULTILINE_NOT_SUPPORTED");
                else if (char.IsControl(c))
                    Issue("INVALID_CONTROL");
                else
                    cell.Append(c);
            }
            else if (c == ',')
            {
                EndCell();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r')
                {
                    if (i + 1 >= csv.Length || csv[i + 1] != '\n')
                    {
                        Issue("INVALID_LINE_ENDING");
                        continue;
                    }
                    i++;
                }
                EndCell();
                if (!failed) EndRow();
            }
            else if (c == '"' && cell.Length == 0 && !closedQuote)
            {
                quoted = true;
            }
            else if (c == '"' || closedQuote)
            {
                Issue("INVALID_QUOTING");
            }
            else if (char.IsControl(c))
            {
                Issue("INVALID_CONTROL");
            }
            else
            {
                cell.Append(c);
            }

            if (cell.Length > MaxCharactersPerCell && !failed)
                Issue("FIELD_TOO_LONG");
        }

        if (failed) return dataRows;
        if (quoted)
        {
            Issue("UNTERMINATED_QUOTE");
            return dataRows;
        }

        // Only an unterminated final row needs processing. Files ending with
        // CRLF/LF have already had their last row processed in the loop.
        if (cells.Count > 0 || cell.Length > 0 || closedQuote ||
            (csv.Length > atStart && csv[^1] == ','))
        {
            EndCell();
            if (!failed) EndRow();
        }
        else if (row == 1)
            add(sheet, 1, "headers", "HEADER_MISMATCH");

        return dataRows;
    }
}
