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
    public void Library_magazine_grant_never_elevates_to_unscoped_write_or_other_category()
    {
        var today = new DateOnly(2026, 10, 20);
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var profile = new DynamicProfile(
            Guid.NewGuid(), "revistas", "Editor de revistas", "order",
            false, true, ["documents"],
            [new DynamicGrant("library", ["view", "create", "edit"], ["magazine"])]);
        catalog.Profiles.Add(profile);
        catalog.Assignments.Add(new DynamicAssignment(
            Guid.NewGuid(), "comunicaciones", "revistas", null,
            today.AddDays(-1), today.AddDays(1), true));

        bool Scoped(string category, string action = "edit", DateOnly? date = null) =>
            DynamicAccessEndpoints.IsCategoryGranted(
                catalog, "comunicaciones", "library", action, category, null, date ?? today);

        Assert.True(Scoped("magazine"));
        Assert.True(Scoped("magazine", "create"));
        Assert.True(Scoped("magazine", "view"));
        Assert.False(Scoped("book"));
        Assert.False(Scoped("official_document"));
        Assert.False(Scoped("revista")); // las APIs normalizan antes de evaluar
        Assert.False(Scoped("magazine", "delete"));
        Assert.False(Scoped("magazine", date: today.AddDays(2)));
        Assert.False(DynamicAccessEndpoints.IsCategoryGranted(
            catalog, "comunicaciones", "documentmanager", "edit", "magazine", null, today));
        Assert.False(DynamicAccessEndpoints.IsCategoryGranted(
            catalog, "otro", "library", "edit", "magazine", null, today));

        Assert.True(DynamicAccessEndpoints.IsGranted(
            catalog, "comunicaciones", "library", "view", null, today));
        Assert.False(DynamicAccessEndpoints.IsGranted(
            catalog, "comunicaciones", "library", "edit", null, today));

        // El grant histórico sin categorías no se interpreta como delegación de categoría.
        catalog.Profiles[^1] = profile with
        {
            Grants = [new DynamicGrant("library", ["view", "edit"])]
        };
        Assert.False(Scoped("magazine"));
        Assert.True(DynamicAccessEndpoints.IsGranted(
            catalog, "comunicaciones", "library", "edit", null, today));
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
    [Fact]
    public void Library_search_is_independent_from_catalog_read_for_assigned_profile()
    {
        var c = DynamicAccessEndpoints.CreateCatalog();
        var today = new DateOnly(2026, 10, 21);
        var profile = new DynamicProfile(Guid.NewGuid(), "library-catalogue",
            "Lectura de biblioteca", "order", false, true,
            ["documents"], [new DynamicGrant("library", ["view"])]);
        c.Profiles.Add(profile);
        c.Assignments.Add(new DynamicAssignment(Guid.NewGuid(), "reader",
            profile.Code, null, today, null, true));
        Assert.True(DynamicViewAccess.Allows(c, "reader", "library", "view", null, today));
        Assert.False(DynamicViewAccess.Allows(c, "reader", "library", "search", null, today));
        c.Profiles[^1] = profile with
        {
            Grants = [new DynamicGrant("library", ["view", "search"])]
        };
        Assert.True(DynamicViewAccess.Allows(c, "reader", "library", "search", null, today));
        c.Assignments[0] = c.Assignments[0] with { IsActive = false };
        Assert.False(DynamicViewAccess.Allows(c, "reader", "library", "search", null, today));
    }

    [Fact]
    public void Search_requires_an_explicit_grant_and_never_inherits_read_permission()
    {
        var catalog = DynamicAccessEndpoints.CreateCatalog();
        var lodge = Guid.NewGuid();
        var today = new DateOnly(2026, 10, 20);
        var profile = new DynamicProfile(Guid.NewGuid(), "admissions-search", "Buscar inscritos",
            "lodge", false, true, ["admissions"],
            [new DynamicGrant("admissions", ["view"])]);
        catalog.Profiles.Add(profile);
        catalog.Assignments.Add(new DynamicAssignment(Guid.NewGuid(), "subject-search",
            profile.Code, lodge, today, today, true));

        bool Allowed(string subject, string action, Guid org, DateOnly when) =>
            DynamicViewAccess.Allows(catalog, subject, "admissions", action, org, when);
        Assert.Contains("search", DynamicAccessEndpoints.AllowedActions);
        Assert.True(Allowed("subject-search", "view", lodge, today));
        Assert.False(Allowed("subject-search", "search", lodge, today));
        catalog.Profiles[^1] = profile with
        {
            Grants = [new DynamicGrant("admissions", ["view", "search"])]
        };
        Assert.True(Allowed("subject-search", "search", lodge, today));
        Assert.True(Allowed("other", "search", lodge, today)); // Unassigned compatibility; domain RBAC still applies.
        Assert.False(DynamicAccessEndpoints.IsGranted(catalog,
            "subject-search", "admissions", "search", Guid.NewGuid(), today));
        Assert.False(DynamicAccessEndpoints.IsGranted(catalog,
            "subject-search", "admissions", "search", lodge, today.AddDays(1)));
        // Search by itself never passes IsGranted without a companion View.
        catalog.Profiles[^1] = profile with
        {
            Grants = [new DynamicGrant("admissions", ["search"])]
        };
        Assert.False(DynamicAccessEndpoints.IsGranted(
            catalog, "subject-search", "admissions", "search", lodge, today));
    }

}
