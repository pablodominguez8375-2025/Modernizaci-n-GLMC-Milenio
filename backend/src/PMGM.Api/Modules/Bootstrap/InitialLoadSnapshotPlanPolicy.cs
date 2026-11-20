namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Redacted, deterministic summary of an already-resolved database snapshot.
/// The caller must query PostgreSQL in a consistent read-only transaction and
/// provide one match per submitted row; no personal values enter this policy.
/// A valid summary does not grant permission to apply or reserve a ledger row.
/// </summary>
public static class InitialLoadSnapshotPlanPolicy
{
    public sealed record Summary(
        bool Valid, string Code, int Rows, int NewRows, int ExactSkips,
        int Conflicts, int Unresolved, int Ambiguous,
        bool CanApply = false, int WritesPerformed = 0);

    public static Summary Inspect(
        IReadOnlyList<InitialLoadExistingRecordPolicy.Match>? matches,
        int expectedRows)
    {
        static Summary Denied(string code) =>
            new(false, code, 0, 0, 0, 0, 0, 0);

        if (expectedRows < 0 || matches is null ||
            matches.Count != expectedRows)
            return Denied("SNAPSHOT_ROW_COUNT_MISMATCH");

        var added = 0;
        var skipped = 0;
        var conflicts = 0;
        var unresolved = 0;
        var ambiguous = 0;
        foreach (var match in matches)
        {
            switch (match)
            {
                case InitialLoadExistingRecordPolicy.Match.Absent: added++; break;
                case InitialLoadExistingRecordPolicy.Match.Exact: skipped++; break;
                case InitialLoadExistingRecordPolicy.Match.Contradictory: conflicts++; break;
                case InitialLoadExistingRecordPolicy.Match.Unresolved: unresolved++; break;
                case InitialLoadExistingRecordPolicy.Match.Ambiguous: ambiguous++; break;
                default: return Denied("INVALID_MATCH_STATE");
            }
        }

        var valid = conflicts == 0 && unresolved == 0 && ambiguous == 0;
        return new(valid, valid ? "SNAPSHOT_PLAN_VALID" : "SNAPSHOT_PLAN_BLOCKED",
            expectedRows, added, skipped, conflicts, unresolved, ambiguous);
    }
}
