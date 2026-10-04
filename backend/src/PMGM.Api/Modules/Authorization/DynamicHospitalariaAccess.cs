using System.Security.Claims;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Authorization;

// Technical restriction only; handlers retain institutional and Council authority checks.
public static class DynamicHospitalariaAccess
{
    public static bool IsManaged(DynamicAccessCatalog catalog, string? subject, Guid organizationId)
        => subject is not null && catalog.Assignments.Any(a => a.Subject == subject && a.OrganizationId == organizationId);
    public static bool Allows(DynamicAccessCatalog catalog, string? subject, string action, Guid organizationId, DateOnly today)
        => (action is "view" or "create" or "write") && (!IsManaged(catalog, subject, organizationId) ||
            DynamicAccessEndpoints.IsGranted(catalog, subject, "hospitalaria", action, organizationId, today));
    public static async Task<bool> AllowsAsync(PmgmDbContext db, ClaimsPrincipal user, Guid organizationId, string action, CancellationToken ct)
        => Allows(await DynamicAccessEndpoints.LoadAsync(db, ct), DynamicTreasuryAccess.Subject(user), action, organizationId, DynamicAccessEndpoints.Today());
    public static async Task<HospitalariaAccessDto> ProjectAsync(PmgmDbContext db, ClaimsPrincipal user, Guid organizationId, IInstitutionalAccessService access, CancellationToken ct)
    {
        var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
        var subject = DynamicTreasuryAccess.Subject(user);
        var canManage = access.CanManageLodgeHospitalaria(user, organizationId);
        var canApprove = access.CanApproveLodgeExpenses(user, organizationId);
        var actions = new[] { "view", "create", "write" }.Where(action =>
            (action == "view" ? access.CanReadLodgeHospitalaria(user, organizationId) : action == "create" ? canManage : canManage || canApprove) &&
            Allows(catalog, subject, action, organizationId, DynamicAccessEndpoints.Today())).ToArray();
        return new(catalog.Version, organizationId, IsManaged(catalog, subject, organizationId), actions);
    }
}
public sealed record HospitalariaAccessDto(int Version, Guid OrganizationId, bool Managed, string[] Actions);
