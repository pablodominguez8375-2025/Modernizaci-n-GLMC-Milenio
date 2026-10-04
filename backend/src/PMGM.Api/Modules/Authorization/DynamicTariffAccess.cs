using System.Security.Claims;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Authorization;

// This slice covers decree administration, not every Gran Tesorería operation.
public static class DynamicTariffAccess
{
    public static bool IsManaged(DynamicAccessCatalog catalog, string? subject)
        => subject is not null && catalog.Assignments.Any(a => a.Subject == subject && a.OrganizationId is null);

    public static bool Allows(DynamicAccessCatalog catalog, string? subject, string action, DateOnly today)
        => (action is "view" or "create") && (!IsManaged(catalog, subject) ||
            DynamicAccessEndpoints.IsGranted(catalog, subject, "treasury", action, null, today));

    public static async Task<bool> AllowsAsync(PmgmDbContext db, ClaimsPrincipal user, string action, CancellationToken ct)
        => Allows(await DynamicAccessEndpoints.LoadAsync(db, ct), DynamicTreasuryAccess.Subject(user), action, DynamicAccessEndpoints.Today());

    public static async Task<TariffAccessDto> ProjectAsync(PmgmDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
        var subject = DynamicTreasuryAccess.Subject(user);
        var today = DynamicAccessEndpoints.Today();
        return new(catalog.Version, IsManaged(catalog, subject),
            new[] { "view", "create" }.Where(action => Allows(catalog, subject, action, today)).ToArray());
    }
}

public sealed record TariffAccessDto(int Version, bool Managed, string[] Actions);
