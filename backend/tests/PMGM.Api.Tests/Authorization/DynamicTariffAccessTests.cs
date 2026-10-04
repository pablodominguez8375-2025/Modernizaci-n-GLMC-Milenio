using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class DynamicTariffAccessTests
{
    [Fact]
    public void Order_decree_grants_are_scope_bound_and_fail_closed_after_enrollment()
    {
        var c = DynamicAccessEndpoints.CreateCatalog(); var today = new DateOnly(2026, 10, 4);
        bool Allowed(string action) => DynamicTariffAccess.Allows(c, "qa", action, today);
        Assert.True(Allowed("create")); Assert.False(Allowed("write"));
        c.Profiles.Add(new(Guid.NewGuid(), "consulta", "Consulta", "order", false, true, ["treasury"], [new("treasury", ["view"])]));
        c.Assignments.Add(new(Guid.NewGuid(), "qa", "consulta", null, today, today, true));
        Assert.True(Allowed("view")); Assert.False(Allowed("create"));
        c.Profiles[ c.Profiles.Count - 1 ] = c.Profiles.Last() with { Grants = [new("treasury", ["view", "create"])] };
        Assert.True(Allowed("create"));
        c.Assignments[0] = c.Assignments[0] with { EffectiveTo = today.AddDays(-1) }; Assert.False(Allowed("view"));
        c.Assignments[0] = c.Assignments[0] with { EffectiveTo = null, EffectiveFrom = today.AddDays(1) }; Assert.False(Allowed("view"));
        c.Assignments[0] = c.Assignments[0] with { EffectiveFrom = today, IsActive = false }; Assert.False(Allowed("view"));
        c.Assignments[0] = c.Assignments[0] with { IsActive = true };
        c.Profiles[c.Profiles.Count - 1] = c.Profiles.Last() with { IsActive = false }; Assert.False(Allowed("view"));
        c.Profiles[c.Profiles.Count - 1] = c.Profiles.Last() with { IsActive = true, Scope = "lodge" }; Assert.False(Allowed("create"));
        Assert.True(DynamicTariffAccess.Allows(c, "other", "view", today));
    }
}
