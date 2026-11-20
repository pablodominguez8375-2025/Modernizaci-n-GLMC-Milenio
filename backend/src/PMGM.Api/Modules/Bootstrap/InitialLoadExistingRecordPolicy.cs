namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Fail-closed, side-effect-free classification of a single initial-load
/// natural key against a database snapshot. The caller must resolve keys and
/// compare canonical persisted fields inside a consistent DB transaction.
/// This policy receives no identity values and never authorizes writes.
/// </summary>
public static class InitialLoadExistingRecordPolicy
{
    public enum Match { Absent, Exact, Contradictory, Unresolved, Ambiguous }

    public sealed record Decision(
        string Code, bool CanSkip, bool BlocksBatch,
        bool CanApply = false, int WritesPerformed = 0);

    public static Decision Inspect(Match match) => match switch
    {
        Match.Absent => new("NEW_RECORD_REQUIRES_TRANSACTION", false, false),
        Match.Exact => new("EXACT_EXISTING_RECORD_SKIP", true, false),
        Match.Contradictory => new("NATURAL_KEY_CONFLICT", false, true),
        Match.Unresolved => new("REFERENCE_UNRESOLVED", false, true),
        Match.Ambiguous => new("REFERENCE_AMBIGUOUS", false, true),
        _ => new("INVALID_MATCH_STATE", false, true)
    };

    /// <summary>
    /// A preview plan is only safe to report if all references are resolved
    /// and there are no contradictory/ambiguous keys. A safe plan is not GO.
    /// </summary>
    public static bool CanPresentPlan(IEnumerable<Match>? matches) =>
        matches is not null && matches.All(m => !Inspect(m).BlocksBatch);
}
