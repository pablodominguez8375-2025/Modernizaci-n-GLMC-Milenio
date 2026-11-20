using System.Globalization;

namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 E3 — pure, fail-closed validation of one CargosVigentes row. The period is NEVER derived
/// from startDate: it must be explicit in the contract (column <c>period</c>). startDate is strict
/// ISO yyyy-MM-dd. officeCode is matched later, exactly, against active OfficeDefinition rows.
/// Codes only; submitted values are never echoed.
/// </summary>
public static class InitialLoadOfficeRowPolicy
{
    public sealed record Row(string PersonRef, string WorkshopCode, string OfficeCode, string StartDate, string Period);

    public sealed record Validated(string PersonRef, string WorkshopCode, string OfficeCode, DateOnly StartDate, string Period);

    public sealed record Finding(int Row, string Column, string Code);

    public sealed record Result(bool Valid, IReadOnlyList<Validated> Rows, IReadOnlyList<Finding> Findings);

    public static Result Inspect(IReadOnlyList<Row>? rows)
    {
        var findings = new List<Finding>();
        var valid = new List<Validated>();
        if (rows is null) return new Result(false, valid, [new Finding(0, "rows", "BATCH_EMPTY")]);
        // An empty Cargos sheet is legitimate (no offices to import).
        var keys = new HashSet<(string, string, string, string)>();
        for (var i = 0; i < rows.Count; i++)
        {
            var n = i + 1;
            var r = rows[i];
            var before = findings.Count;
            if (r is null) { findings.Add(new Finding(n, "row", "ROW_NULL")); continue; }
            if (string.IsNullOrWhiteSpace(r.PersonRef)) findings.Add(new Finding(n, "personRef", "REQUIRED"));
            if (string.IsNullOrWhiteSpace(r.WorkshopCode)) findings.Add(new Finding(n, "workshopCode", "REQUIRED"));
            if (string.IsNullOrWhiteSpace(r.OfficeCode)) findings.Add(new Finding(n, "officeCode", "REQUIRED"));
            if (string.IsNullOrWhiteSpace(r.Period) || r.Period.Trim().Length > 80)
                findings.Add(new Finding(n, "period", "PERIOD_NOT_EXPLICIT"));
            if (!DateOnly.TryParseExact(r.StartDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var start))
                findings.Add(new Finding(n, "startDate", "INVALID_DATE"));

            if (findings.Count == before &&
                !keys.Add((r.PersonRef.Trim(), r.WorkshopCode.Trim(), r.OfficeCode.Trim(), r.Period.Trim())))
                findings.Add(new Finding(n, "row", "DUPLICATE_OFFICE_ROW"));

            if (findings.Count == before)
                valid.Add(new Validated(r.PersonRef.Trim(), r.WorkshopCode.Trim(), r.OfficeCode.Trim(), start, r.Period.Trim()));
        }
        return new Result(findings.Count == 0, findings.Count == 0 ? valid : [], findings);
    }
}
