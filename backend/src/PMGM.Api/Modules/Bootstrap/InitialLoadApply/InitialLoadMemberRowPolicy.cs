using PMGM.Api.Modules.SecretariatOperations;

namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 E2 — pure, fail-closed validation of one Hermanos row (provisional matrix, ChatGPT 7046394578).
/// Nothing is guessed: grade must be an exact canonical code, only RUT identities are accepted (valid
/// check digit), and an institutionalStatus is accepted only when it maps to a persisted state
/// WITHOUT inventing an event date. Today only "active" qualifies (Membership.Status = active);
/// every other value is a per-row error and makes the whole batch non-applicable.
/// Returns stable codes only; submitted values are never echoed.
/// </summary>
public static class InitialLoadMemberRowPolicy
{
    public static readonly IReadOnlySet<string> CanonicalGrades =
        new HashSet<string>(StringComparer.Ordinal) { "apprentice", "fellowcraft", "master" };

    public static readonly IReadOnlySet<string> AdmissibleInstitutionalStatuses =
        new HashSet<string>(StringComparer.Ordinal) { "active" };

    public sealed record Row(
        string PersonRef, string IdentityType, string IdentityNumber,
        string FirstNames, string LastNames, string? Email,
        string WorkshopCode, string Grade, string InstitutionalStatus);

    public sealed record Validated(
        string PersonRef, string Rut, string FirstNames, string LastNames, string? Email,
        string WorkshopCode, string Grade);

    public sealed record Finding(int Row, string Column, string Code);

    public sealed record Result(bool Valid, IReadOnlyList<Validated> Rows, IReadOnlyList<Finding> Findings);

    public static Result Inspect(IReadOnlyList<Row>? rows)
    {
        var findings = new List<Finding>();
        var valid = new List<Validated>();
        if (rows is null || rows.Count == 0)
            return new Result(false, valid, [new Finding(0, "rows", "BATCH_EMPTY")]);

        var refs = new HashSet<string>(StringComparer.Ordinal);
        var ruts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < rows.Count; i++)
        {
            var n = i + 1;
            var r = rows[i];
            var before = findings.Count;
            if (r is null) { findings.Add(new Finding(n, "row", "ROW_NULL")); continue; }

            if (string.IsNullOrWhiteSpace(r.PersonRef)) findings.Add(new Finding(n, "personRef", "REQUIRED"));
            else if (!refs.Add(r.PersonRef.Trim())) findings.Add(new Finding(n, "personRef", "DUPLICATE_PERSON_REF"));
            if (string.IsNullOrWhiteSpace(r.WorkshopCode)) findings.Add(new Finding(n, "workshopCode", "REQUIRED"));
            if (string.IsNullOrWhiteSpace(r.FirstNames) || r.FirstNames.Trim().Length > 160)
                findings.Add(new Finding(n, "firstNames", "INVALID"));
            if (string.IsNullOrWhiteSpace(r.LastNames) || r.LastNames.Trim().Length > 160)
                findings.Add(new Finding(n, "lastNames", "INVALID"));
            if (!string.IsNullOrWhiteSpace(r.Email) &&
                (r.Email.Trim().Length > 320 || !r.Email.Contains('@') || r.Email.Any(char.IsWhiteSpace)))
                findings.Add(new Finding(n, "email", "INVALID"));

            // Only RUT is a typed identity today; any other document is rejected explicitly.
            string? rut = null;
            if (!string.Equals(r.IdentityType?.Trim(), "RUT", StringComparison.OrdinalIgnoreCase))
                findings.Add(new Finding(n, "identityType", "IDENTITY_TYPE_UNSUPPORTED"));
            else if (!SecretariatOperationsPolicy.IsValidRut(r.IdentityNumber))
                findings.Add(new Finding(n, "identityNumber", "INVALID_RUT"));
            else
            {
                rut = SecretariatOperationsPolicy.NormalizeRut(r.IdentityNumber)!;
                if (ruts.ContainsKey(rut)) findings.Add(new Finding(n, "identityNumber", "DUPLICATE_RUT_IN_BATCH"));
                else ruts[rut] = n;
            }

            if (r.Grade is null || !CanonicalGrades.Contains(r.Grade))
                findings.Add(new Finding(n, "grade", "GRADE_NOT_CANONICAL"));
            if (r.InstitutionalStatus is null || !AdmissibleInstitutionalStatuses.Contains(r.InstitutionalStatus))
                findings.Add(new Finding(n, "institutionalStatus", "STATUS_NOT_ADMISSIBLE"));

            if (findings.Count == before && rut is not null)
                valid.Add(new Validated(r.PersonRef.Trim(), rut, r.FirstNames.Trim(), r.LastNames.Trim(),
                    string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim(),
                    r.WorkshopCode.Trim(), r.Grade!));
        }

        return new Result(findings.Count == 0, findings.Count == 0 ? valid : [], findings);
    }
}
