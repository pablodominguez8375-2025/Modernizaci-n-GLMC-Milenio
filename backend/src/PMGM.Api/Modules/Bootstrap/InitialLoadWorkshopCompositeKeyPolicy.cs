namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Candidate database identity for workshops: (Number, OrienteCode).
/// workshopCode is only a CSV-local reference, never a persisted identity.
/// Pure and fail-closed: caller must verify the canonical key contract before use.
/// Does not authorize writes.
/// </summary>
public static class InitialLoadWorkshopCompositeKeyPolicy
{
    public static InitialLoadExistingRecordPolicy.Match Inspect(
        InitialLoadWorkshopMatchPolicy.Incoming? incoming,
        IReadOnlyList<InitialLoadWorkshopMatchPolicy.Existing>? snapshot)
    {
        if (incoming is null || snapshot is null ||
            string.IsNullOrWhiteSpace(incoming.Code) ||
            string.IsNullOrWhiteSpace(incoming.Number) ||
            string.IsNullOrWhiteSpace(incoming.OrienteCode))
            return InitialLoadExistingRecordPolicy.Match.Unresolved;

        // Unknown or incomplete persisted keys can conceal a collision.
        if (snapshot.Any(x => x is null ||
            string.IsNullOrWhiteSpace(x.Number) ||
            string.IsNullOrWhiteSpace(x.OrienteCode)))
            return InitialLoadExistingRecordPolicy.Match.Unresolved;

        var duplicates = snapshot.GroupBy(x => (x.Number, x.OrienteCode))
            .Any(g => g.Count() > 1);
        if (duplicates)
            return InitialLoadExistingRecordPolicy.Match.Ambiguous;

        var found = snapshot.Where(x =>
            string.Equals(x.Number, incoming.Number, StringComparison.Ordinal) &&
            string.Equals(x.OrienteCode, incoming.OrienteCode, StringComparison.Ordinal))
            .Take(2).ToArray();

        if (found.Length == 0)
            return InitialLoadExistingRecordPolicy.Match.Absent;
        if (found.Length > 1)
            return InitialLoadExistingRecordPolicy.Match.Ambiguous;

        var row = found[0];
        if (string.IsNullOrWhiteSpace(row.Name) ||
            string.IsNullOrWhiteSpace(row.City) ||
            string.IsNullOrWhiteSpace(row.Country))
            return InitialLoadExistingRecordPolicy.Match.Unresolved;

        return string.Equals(row.Name, incoming.Name, StringComparison.Ordinal) &&
               string.Equals(row.City, incoming.City, StringComparison.Ordinal) &&
               string.Equals(row.Country, incoming.Country, StringComparison.Ordinal)
            ? InitialLoadExistingRecordPolicy.Match.Exact
            : InitialLoadExistingRecordPolicy.Match.Contradictory;
    }
}
