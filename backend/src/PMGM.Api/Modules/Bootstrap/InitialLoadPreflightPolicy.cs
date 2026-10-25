using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using PMGM.Api.Modules.Core;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Offline, side-effect-free structural policy for future initial-load dry-run.
/// No HTTP route calls this policy. No values (especially identity numbers,
/// names, emails or supplied organization codes) are returned in diagnostics.
/// A successful preflight is NEVER authorization to apply.
/// </summary>
public static class InitialLoadPreflightPolicy
{
    public const int MaxRowsPerSheet = 2000;
    public const int MaxFieldLength = 256;
    public const int MaxIssues = 100;

    public sealed record Finding(string Sheet, int Row, string Field, string Code);
    public sealed record Result(
        bool Valid, bool CanApply, int WritesPerformed,
        IReadOnlyList<Finding> Findings);

    public static Result Inspect(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>> sheets)
    {
        var findings = new List<Finding>();
        var invalid = false;
        void Add(string sheet, int row, string field, string code)
        {
            invalid = true;
            if (findings.Count < MaxIssues)
                findings.Add(new Finding(sheet, row, field, code));
        }

        var allowed = InitialLoadTemplateEndpoints.Sheets.Select(s => s.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (sheets is null || sheets.Count != allowed.Count ||
            !sheets.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(allowed))
        {
            Add("schema", 0, "files", "INVALID_SHEETS");
            return new Result(false, false, 0, findings);
        }

        foreach (var schema in InitialLoadTemplateEndpoints.Sheets)
        {
            if (!sheets.TryGetValue(schema.Name, out var rows) || rows is null ||
                rows.Count > MaxRowsPerSheet)
            {
                Add(schema.Name, 0, "rows", "INVALID_ROW_COUNT");
                return new Result(false, false, 0, findings);
            }
        }

        var workshops = new HashSet<string>(StringComparer.Ordinal);
        var persons = new Dictionary<string, string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var officeCodes = BootstrapCodes.WorkshopOffices.Select(o => o.Code)
            .ToHashSet(StringComparer.Ordinal);

        string Read(IReadOnlyDictionary<string, string> row, string key)
            => row.TryGetValue(key, out var value) ? (value ?? string.Empty).Trim() : string.Empty;

        bool Required(string sheet, int index, IReadOnlyDictionary<string, string> row, string key)
        {
            if (!string.IsNullOrWhiteSpace(Read(row, key))) return true;
            Add(sheet, index + 2, key, "REQUIRED");
            return false;
        }

        bool Code(string sheet, int index, IReadOnlyDictionary<string, string> row, string key)
        {
            if (!Required(sheet, index, row, key)) return false;
            if (Regex.IsMatch(Read(row, key), @"\A[A-Za-z0-9_-]{1,32}\z",
                RegexOptions.CultureInvariant)) return true;
            Add(sheet, index + 2, key, "INVALID_CODE");
            return false;
        }

        void ValidateShape(string sheet, int index, IReadOnlyDictionary<string, string> row,
            string[] expected)
        {
            if (row is null || !row.Keys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(expected))
                Add(sheet, index + 2, "columns", "INVALID_COLUMNS");
            if (row is null) return;
            foreach (var column in expected)
            {
                if (Read(row, column).Length > MaxFieldLength)
                    Add(sheet, index + 2, column, "FIELD_TOO_LONG");
            }
        }

        const string workshopFile = "Talleres.csv";
        const string personFile = "Hermanos.csv";
        const string officeFile = "CargosVigentes.csv";
        var workshopSchema = InitialLoadTemplateEndpoints.Sheets[0].Columns;
        var personSchema = InitialLoadTemplateEndpoints.Sheets[1].Columns;
        var officeSchema = InitialLoadTemplateEndpoints.Sheets[2].Columns;

        var ws = sheets[workshopFile];
        for (var i = 0; i < ws.Count; i++)
        {
            var row = ws[i];
            if (row is null) { Add(workshopFile, i + 2, "columns", "INVALID_COLUMNS"); continue; }
            ValidateShape(workshopFile, i, row, workshopSchema);
            if (Code(workshopFile, i, row, "workshopCode") &&
                !workshops.Add(Read(row, "workshopCode")))
                Add(workshopFile, i + 2, "workshopCode", "DUPLICATE");
            foreach (var key in new[] { "number", "name", "city", "country" })
                Required(workshopFile, i, row, key);
        }

        var ps = sheets[personFile];
        for (var i = 0; i < ps.Count; i++)
        {
            var row = ps[i];
            if (row is null) { Add(personFile, i + 2, "columns", "INVALID_COLUMNS"); continue; }
            ValidateShape(personFile, i, row, personSchema);
            var reference = Read(row, "personRef");
            if (Code(personFile, i, row, "personRef"))
            {
                if (!persons.TryAdd(reference, Read(row, "workshopCode")))
                    Add(personFile, i + 2, "personRef", "DUPLICATE");
            }
            if (Code(personFile, i, row, "workshopCode") &&
                !workshops.Contains(Read(row, "workshopCode")))
                Add(personFile, i + 2, "workshopCode", "UNKNOWN_WORKSHOP");
            foreach (var key in new[] { "firstNames", "lastNames", "institutionalStatus" })
                Required(personFile, i, row, key);

            var grade = Read(row, "grade");
            if (!new[] { "1", "2", "3" }.Contains(grade, StringComparer.Ordinal))
                Add(personFile, i + 2, "grade", "INVALID_GRADE");

            var email = Read(row, "email");
            if (string.IsNullOrWhiteSpace(email) ||
                !MailAddress.TryCreate(email, out var address) || address.Address != email)
                Add(personFile, i + 2, "email", "INVALID_EMAIL");

            var type = Read(row, "identityType");
            var number = Read(row, "identityNumber");
            string? normalized = null;
            if (type == "rut")
            {
                if (ChileanRut.TryNormalize(number, out var rut, out _))
                    normalized = "rut:" + rut;
            }
            else if (type == "passport")
            {
                if (Regex.IsMatch(number, @"\A[A-Za-z0-9]{4,24}\z",
                    RegexOptions.CultureInvariant))
                    normalized = "passport:" + number.ToUpperInvariant();
            }
            else Add(personFile, i + 2, "identityType", "INVALID_IDENTITY_TYPE");

            if (normalized is null)
                Add(personFile, i + 2, "identityNumber", "INVALID_IDENTITY");
            else if (!identities.Add(normalized))
                Add(personFile, i + 2, "identityNumber", "DUPLICATE");
        }

        var os = sheets[officeFile];
        for (var i = 0; i < os.Count; i++)
        {
            var row = os[i];
            if (row is null) { Add(officeFile, i + 2, "columns", "INVALID_COLUMNS"); continue; }
            ValidateShape(officeFile, i, row, officeSchema);
            var reference = Read(row, "personRef");
            var shop = Read(row, "workshopCode");
            if (Code(officeFile, i, row, "personRef") && !persons.ContainsKey(reference))
                Add(officeFile, i + 2, "personRef", "UNKNOWN_PERSON");
            if (Code(officeFile, i, row, "workshopCode") && !workshops.Contains(shop))
                Add(officeFile, i + 2, "workshopCode", "UNKNOWN_WORKSHOP");
            if (persons.TryGetValue(reference, out var memberShop) &&
                !string.Equals(shop, memberShop, StringComparison.Ordinal))
                Add(officeFile, i + 2, "workshopCode", "PERSON_WORKSHOP_MISMATCH");
            if (!officeCodes.Contains(Read(row, "officeCode")))
                Add(officeFile, i + 2, "officeCode", "UNKNOWN_OFFICE");
            if (!DateOnly.TryParseExact(Read(row, "startDate"), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                Add(officeFile, i + 2, "startDate", "INVALID_DATE");
        }

        return new Result(!invalid, false, 0, findings);
    }
}
