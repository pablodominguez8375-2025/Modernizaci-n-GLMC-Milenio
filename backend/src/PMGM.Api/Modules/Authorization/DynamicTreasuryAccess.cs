using System.Security.Claims;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Authorization;

// Additional technical restriction; domain handlers still enforce their institutional authority.
public static class DynamicTreasuryAccess
{
    public const string View = "lodgetreasury";
    public static string? Subject(ClaimsPrincipal user) => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
    public static bool IsManaged(DynamicAccessCatalog catalog, string? subject, Guid organizationId)
        => subject is not null && catalog.Assignments.Any(a => a.Subject == subject && a.OrganizationId == organizationId);
    public static bool Allows(DynamicAccessCatalog catalog, string? subject, string action, Guid organizationId, DateOnly today)
        => DynamicAccessEndpoints.AllowedActions.Contains(action) && (!IsManaged(catalog, subject, organizationId) ||
            DynamicAccessEndpoints.IsGranted(catalog, subject, View, action, organizationId, today));
    public static async Task<bool> AllowsAsync(PmgmDbContext db, ClaimsPrincipal user, Guid organizationId, string action, CancellationToken ct)
        => Allows(await DynamicAccessEndpoints.LoadAsync(db, ct), Subject(user), action, organizationId, DynamicAccessEndpoints.Today());
    public static async Task<TreasuryAccessDto> ProjectAsync(PmgmDbContext db, ClaimsPrincipal user, Guid organizationId, IInstitutionalAccessService access, CancellationToken ct)
    {
        var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
        var canManage = access.CanManageLodgeTreasury(user, organizationId);
        var canApprove = access.CanApproveLodgeExpenses(user, organizationId);
        var actions = DynamicAccessEndpoints.AllowedActions.Where(action =>
            (canManage || (canApprove && (action is "view" or "write" or "print"))) &&
            Allows(catalog, Subject(user), action, organizationId, DynamicAccessEndpoints.Today())).ToArray();
        return new(catalog.Version, organizationId, IsManaged(catalog, Subject(user), organizationId), actions);
    }
}
public sealed record TreasuryAccessDto(int Version, Guid OrganizationId, bool Managed, string[] Actions);
