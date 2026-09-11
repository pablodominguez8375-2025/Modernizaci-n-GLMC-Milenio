using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class GrandMasterCeremonyAuthorizationPolicyTests
{
    [Fact]
    public void MissingApproval_ReturnsFalse()
    {
        Assert.False(GrandMasterCeremonyAuthorizationPolicy.CanAuthorize(null));
    }

    [Fact]
    public void Approved_ReturnsTrue()
    {
        Assert.True(GrandMasterCeremonyAuthorizationPolicy.CanAuthorize(CeremonyCodes.ValidationStatus.Approved));
    }

    [Fact]
    public void Observed_ReturnsFalse()
    {
        Assert.False(GrandMasterCeremonyAuthorizationPolicy.CanAuthorize(CeremonyCodes.ValidationStatus.Observed));
    }
}
