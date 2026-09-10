using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class MemberTreasuryRulesTests
{
    [Theory]
    [InlineData(MembershipCodes.InstitutionalStatus.PastActive)]
    [InlineData(MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal)]
    [InlineData(MembershipCodes.InstitutionalStatus.ForcedWithdrawal)]
    [InlineData(MembershipCodes.InstitutionalStatus.Deceased)]
    [InlineData(MembershipCodes.InstitutionalStatus.Inactive)]
    public void Ordinary_dues_are_blocked_for_non_billable_institutional_states(string status)
    {
        Assert.True(MemberTreasuryEndpoints.BlocksOrdinaryDues(status));
    }

    [Theory]
    [InlineData(MembershipCodes.InstitutionalStatus.Active)]
    [InlineData(MembershipCodes.InstitutionalStatus.Reinstated)]
    [InlineData(MembershipCodes.InstitutionalStatus.WorkshopTransfer)]
    [InlineData(null)]
    public void Ordinary_dues_are_not_blocked_for_billable_or_undefined_states(string? status)
    {
        Assert.False(MemberTreasuryEndpoints.BlocksOrdinaryDues(status));
    }
}
