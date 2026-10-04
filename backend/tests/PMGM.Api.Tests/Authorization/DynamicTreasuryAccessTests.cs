using PMGM.Api.Modules.Authorization;
using Xunit;
namespace PMGM.Api.Tests.Authorization;
public sealed class DynamicTreasuryAccessTests
{
    [Fact]
    public void Legacy_access_survives_but_enrolled_revoked_expired_and_wrong_action_access_fail_closed()
    {
        var c = DynamicAccessEndpoints.CreateCatalog(); var org = Guid.NewGuid(); var today = new DateOnly(2026, 10, 4);
        bool Allowed(string action, string subject = "qa") => DynamicTreasuryAccess.Allows(c, subject, action, org, today);
        Assert.True(Allowed("write")); Assert.False(Allowed("export"));
        c.Profiles.Add(new(Guid.NewGuid(), "consulta", "Consulta", "lodge", false, true, ["treasury"], [new("lodgetreasury", ["view", "print"])]));
        c.Assignments.Add(new(Guid.NewGuid(), "qa", "consulta", org, today, today, true));
        Assert.True(Allowed("view")); Assert.True(Allowed("print")); Assert.False(Allowed("write")); Assert.False(Allowed("delete"));
        Assert.True(Allowed("write", "another-subject"));
        c.Assignments[0] = c.Assignments[0] with { IsActive = false };
        Assert.False(Allowed("view")); Assert.False(Allowed("write"));
        c.Assignments[0] = c.Assignments[0] with { IsActive = true, EffectiveTo = today.AddDays(-1) };
        Assert.False(Allowed("view"));
        c.Assignments[0] = c.Assignments[0] with { EffectiveTo = null, EffectiveFrom = today.AddDays(1) };
        Assert.False(Allowed("view"));
    }
}
