namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Redacted all-or-nothing workshop reconciliation. The caller must supply
/// an independently verified canonical code mapping for DB records.
/// This is a pure policy: no database access, no mutations, no HTTP output
/// of submitted or persisted values.
/// </summary>
public static class InitialLoadWorkshopBatchPolicy
{
    public sealed record Result(
        bool Valid, string Code, int NewRows, int ExactSkips,
        int BlockedRows, bool CanApply = false, int WritesPerformed = 0);

    public static Result Inspect(
        IReadOnlyList<InitialLoadWorkshopMatchPolicy.Incoming>? incoming,
        IReadOnlyList<InitialLoadWorkshopMatchPolicy.Existing>? existing)
    {
        static Result Deny(string code, int blocked) =>
            new(false, code, 0, 0, blocked);

        if (incoming is null || existing is null)
            return Deny("SNAPSHOT_UNAVAILABLE", 0);

        // Do not permit a corrupt or noncanonical mapping to make a row look
        // absent. Empty or duplicate DB keys are always an ambiguous snapshot.
        if (existing.Any(x => x is null || string.IsNullOrWhiteSpace(x.Code) ||
            string.IsNullOrWhiteSpace(x.Number) || string.IsNullOrWhiteSpace(x.Name) ||
            string.IsNullOrWhiteSpace(x.City) || string.IsNullOrWhiteSpace(x.Country)) ||
            existing.GroupBy(x => x.Code, StringComparer.Ordinal)
                .Any(g => g.Count() > 1))
            return Deny("SNAPSHOT_KEYS_AMBIGUOUS", existing.Count);

        if (incoming.Any(x => string.IsNullOrWhiteSpace(x.Code)) ||
            incoming.GroupBy(x => x.Code, StringComparer.Ordinal)
                .Any(g => g.Count() > 1))
            return Deny("BATCH_KEYS_INVALID_OR_DUPLICATE", incoming.Count);

        var added = 0;
        var skipped = 0;
        var blocked = 0;
        foreach (var row in incoming)
        {
            var match = InitialLoadWorkshopMatchPolicy.Inspect(row, existing);
            switch (match)
            {
                case InitialLoadExistingRecordPolicy.Match.Absent: added++; break;
                case InitialLoadExistingRecordPolicy.Match.Exact: skipped++; break;
                default: blocked++; break;
            }
        }

        return blocked > 0
            ? Deny("BATCH_NATURAL_KEY_CONFLICT", blocked)
            : new Result(true, "BATCH_PLAN_VALID", added, skipped, 0);
    }
}
