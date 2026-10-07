using PMGM.Api.Modules.Authorization;
using Xunit;
namespace PMGM.Api.Tests.Authorization;
public sealed class DynamicGrandTreasuryAccessTests
{
    [Fact]
    public void Order_enrollment_never_falls_back_after_revocation_expiry_or_profile_deactivation()
    {
        var c = DynamicAccessEndpoints.CreateCatalog(); var today = new DateOnly(2026, 10, 6);
        bool Allowed(string action) => DynamicGrandTreasuryAccess.Allows(c, "qa", action, today);
        Assert.True(Allowed("write")); Assert.False(Allowed("delete"));
        c.Profiles.Add(new(Guid.NewGuid(), "qa-order", "Consulta", "order", false, true, ["treasury"], [new("treasury", ["view"])]));
        c.Assignments.Add(new(Guid.NewGuid(), "qa", "qa-order", null, today, today, true));
        Assert.True(Allowed("view")); Assert.False(Allowed("create")); Assert.False(Allowed("write"));
        c.Assignments[0] = c.Assignments[0] with { IsActive = false }; Assert.False(Allowed("view"));
        c.Assignments[0] = c.Assignments[0] with { IsActive = true, EffectiveTo = today.AddDays(-1) }; Assert.False(Allowed("view"));
        c.Assignments[0] = c.Assignments[0] with { EffectiveFrom = today.AddDays(1), EffectiveTo = null }; Assert.False(Allowed("view"));
        c.Assignments[0] = c.Assignments[0] with { EffectiveFrom = today };
        c.Profiles[^1] = c.Profiles[^1] with { IsActive = false }; Assert.False(Allowed("view"));
    }
    [Fact]
    public void Lodge_grants_cannot_satisfy_a_managed_Order_profile_and_subjects_remain_isolated()
    {
        var c = DynamicAccessEndpoints.CreateCatalog(); var today = new DateOnly(2026, 10, 6);
        c.Profiles.Add(new(Guid.NewGuid(), "qa-lodge", "Taller", "lodge", false, true, ["treasury"], [new("treasury", ["view", "create", "write"])]));
        c.Assignments.Add(new(Guid.NewGuid(), "qa", "qa-lodge", Guid.NewGuid(), today, null, true));
        Assert.False(DynamicGrandTreasuryAccess.IsManaged(c, "qa")); // legacy authority still required by handlers
        c.Profiles.Add(new(Guid.NewGuid(), "qa-order", "Orden sin grant", "order", false, true, ["treasury"], []));
        c.Assignments.Add(new(Guid.NewGuid(), "qa", "qa-order", null, today, null, true));
        Assert.False(DynamicGrandTreasuryAccess.Allows(c, "qa", "view", today));
        Assert.True(DynamicGrandTreasuryAccess.Allows(c, "other", "view", today));
    }
}
