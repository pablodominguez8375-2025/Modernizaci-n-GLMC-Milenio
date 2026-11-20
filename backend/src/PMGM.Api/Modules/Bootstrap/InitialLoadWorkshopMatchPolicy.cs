namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Fail-closed workshop natural-key reconciliation for a future DB-backed plan.
/// The caller MUST supply a verified canonical workshopCode mapping from the
/// persistence model. Never infer that Organization.Number is workshopCode.
/// Values are kept internal and are never included in public diagnostics.
/// </summary>
public static class InitialLoadWorkshopMatchPolicy
{
    public sealed record Existing(string Code, string Number, string Name,
        string OrienteCode, string City, string Country);

    public sealed record Incoming(string Code, string Number, string Name,
        string OrienteCode, string City, string Country);

    public static InitialLoadExistingRecordPolicy.Match Inspect(
        Incoming? incoming, IReadOnlyList<Existing>? snapshot)
    {
        if (incoming is null || snapshot is null ||
            string.IsNullOrWhiteSpace(incoming.Code))
            return InitialLoadExistingRecordPolicy.Match.Unresolved;

        var matches = snapshot.Where(x =>
            string.Equals(x.Code, incoming.Code, StringComparison.Ordinal))
            .Take(2).ToArray();
        if (matches.Length > 1)
            return InitialLoadExistingRecordPolicy.Match.Ambiguous;
        if (matches.Length == 0)
            return InitialLoadExistingRecordPolicy.Match.Absent;

        var found = matches[0];
        return string.Equals(found.Number, incoming.Number, StringComparison.Ordinal) &&
               string.Equals(found.Name, incoming.Name, StringComparison.Ordinal) &&
               string.Equals(found.OrienteCode, incoming.OrienteCode, StringComparison.Ordinal) &&
               string.Equals(found.City, incoming.City, StringComparison.Ordinal) &&
               string.Equals(found.Country, incoming.Country, StringComparison.Ordinal)
            ? InitialLoadExistingRecordPolicy.Match.Exact
            : InitialLoadExistingRecordPolicy.Match.Contradictory;
    }
}
