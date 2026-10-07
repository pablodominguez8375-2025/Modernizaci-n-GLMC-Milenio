using System.Security.Claims;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Authorization;

// Technical restrictions never replace institutional authority. Historical enrollment
// remains managed after revocation/expiry, so it cannot fall back to legacy access.
public static class DynamicGrandTreasuryAccess
{
    public static bool IsManaged(DynamicAccessCatalog catalog, string? subject)
        => subject is not null && catalog.Assignments.Any(a => a.Subject == subject && a.OrganizationId is null);

    public static bool Allows(DynamicAccessCatalog catalog, string? subject, string action, DateOnly today)
        => (action is "view" or "create" or "write") && (!IsManaged(catalog, subject) ||
            DynamicAccessEndpoints.IsGranted(catalog, subject, "treasury", action, null, today));

    public static async Task<bool> AllowsAsync(PmgmDbContext db, ClaimsPrincipal user, string action, CancellationToken ct)
        => Allows(await DynamicAccessEndpoints.LoadAsync(db, ct), DynamicTreasuryAccess.Subject(user), action, DynamicAccessEndpoints.Today());

    public static async Task<GrandTreasuryAccessDto> ProjectAsync(PmgmDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
        var subject = DynamicTreasuryAccess.Subject(user);
        var today = DynamicAccessEndpoints.Today();
        return new(catalog.Version, IsManaged(catalog, subject),
            new[] { "view", "create", "write" }.Where(action => Allows(catalog, subject, action, today)).ToArray());
    }
}

public sealed record GrandTreasuryAccessDto(int Version, bool Managed, string[] Actions);
