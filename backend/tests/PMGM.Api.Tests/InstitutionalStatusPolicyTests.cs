using PMGM.Api.Modules.Membership;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InstitutionalStatusPolicyTests
{
    [Fact]
    public void Past_active_keeps_workshop_roster_but_is_not_operational_and_does_not_generate_dues()
    {
        var status = MembershipCodes.InstitutionalStatus.PastActive;

        Assert.True(InstitutionalStatusPolicy.IsPastActive(status));
        Assert.True(InstitutionalStatusPolicy.KeepsWorkshopRosterMembership(status));
        Assert.False(InstitutionalStatusPolicy.IsOperationallyActive(status));
        Assert.False(InstitutionalStatusPolicy.GeneratesOrdinaryDues(status, hasActiveMembership: true));
    }

    [Fact]
    public void Voluntary_withdrawal_is_not_past_active_and_does_not_generate_dues()
    {
        var status = MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal;

        Assert.False(InstitutionalStatusPolicy.IsPastActive(status));
        Assert.True(InstitutionalStatusPolicy.IsVoluntaryWithdrawal(status));
        Assert.False(InstitutionalStatusPolicy.KeepsWorkshopRosterMembership(status));
        Assert.False(InstitutionalStatusPolicy.IsOperationallyActive(status));
        Assert.False(InstitutionalStatusPolicy.GeneratesOrdinaryDues(status, hasActiveMembership: true));
    }

    [Theory]
    [InlineData("active")]
    [InlineData("reinstated")]
    [InlineData("workshop_transfer")]
    public void Operational_status_with_active_membership_generates_ordinary_dues(string status)
    {
        Assert.True(InstitutionalStatusPolicy.IsOperationallyActive(status));
        Assert.True(InstitutionalStatusPolicy.GeneratesOrdinaryDues(status, hasActiveMembership: true));
        Assert.False(InstitutionalStatusPolicy.GeneratesOrdinaryDues(status, hasActiveMembership: false));
    }

    [Fact]
    public void Workshop_transfer_is_resolved_as_active_when_destination_membership_exists()
    {
        Assert.Equal(
            MembershipCodes.InstitutionalStatus.Active,
            InstitutionalStatusPolicy.ResolveCurrentStatus(
                MembershipCodes.InstitutionalStatus.WorkshopTransfer,
                hasCurrentMembership: true));

        Assert.Equal(
            MembershipCodes.InstitutionalStatus.Inactive,
            InstitutionalStatusPolicy.ResolveCurrentStatus(
                MembershipCodes.InstitutionalStatus.WorkshopTransfer,
                hasCurrentMembership: false));
    }
}
