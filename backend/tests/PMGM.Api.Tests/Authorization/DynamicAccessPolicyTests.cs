using PMGM.Api.Modules.Authorization;
using Xunit;
namespace PMGM.Api.Tests.Authorization;
public sealed class DynamicAccessPolicyTests
{
    [Fact]
    public void Grants_are_exact_subject_organization_date_action_and_active_menu_scoped()
    {
        var org = Guid.NewGuid(); var today = new DateOnly(2026, 10, 4);
        var c = DynamicAccessEndpoints.CreateCatalog();
        c.Profiles.Add(new DynamicProfile(Guid.NewGuid(), "apoyo", "Apoyo", "lodge", false, true, ["personal"], [new DynamicGrant("calendar", ["view", "print"])]));
        c.Assignments.Add(new DynamicAssignment(Guid.NewGuid(), "Subject-A", "apoyo", org, today, today, true));
        bool Grant(string? subject = "Subject-A", string action = "print", Guid? organization = null, DateOnly? date = null) => DynamicAccessEndpoints.IsGranted(c, subject, "calendar", action, organization ?? org, date ?? today);
        Assert.True(Grant()); Assert.False(Grant(subject: "subject-a")); Assert.False(Grant(subject: null)); Assert.False(Grant(action: "edit")); Assert.False(Grant(organization: Guid.NewGuid())); Assert.False(Grant(date: today.AddDays(-1))); Assert.False(Grant(date: today.AddDays(1)));
        c.Menus[0] = c.Menus[0] with { IsActive = false }; Assert.False(Grant());
    }
    [Fact]
    public void Revoked_assignments_inactive_profiles_missing_view_and_unselected_menus_deny()
    {
        var c = DynamicAccessEndpoints.CreateCatalog(); var today = new DateOnly(2026, 10, 4);
        var p = new DynamicProfile(Guid.NewGuid(), "apoyo", "Apoyo", "order", false, true, ["personal"], [new DynamicGrant("calendar", ["print"])]);
        c.Profiles.Add(p); c.Assignments.Add(new DynamicAssignment(Guid.NewGuid(), "s", "apoyo", null, today, null, true));
        bool Grant() => DynamicAccessEndpoints.IsGranted(c, "s", "calendar", "print", null, today);
        Assert.False(Grant()); c.Profiles[^1] = p with { Grants = [new DynamicGrant("calendar", ["view", "print"])] }; Assert.True(Grant());
        var valid = c.Profiles[^1]; c.Profiles[^1] = valid with { MenuCodes = [] }; Assert.False(Grant());
        c.Profiles[^1] = valid with { IsActive = false }; Assert.False(Grant()); c.Profiles[^1] = valid;
        c.Assignments[0] = c.Assignments[0] with { IsActive = false }; Assert.False(Grant());
    }
}
