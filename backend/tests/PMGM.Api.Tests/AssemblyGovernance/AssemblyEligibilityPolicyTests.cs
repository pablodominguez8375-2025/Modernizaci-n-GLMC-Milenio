using PMGM.Api.Modules.AssemblyGovernance;

namespace PMGM.Api.Tests.AssemblyGovernance;

public sealed class AssemblyEligibilityPolicyTests
{
    [Fact]
    public void Enabled_member_can_attend_and_vote()
    {
        var decision = AssemblyEligibilityPolicy.Evaluate(new AssemblyEligibilityInput(true, true, false, false, true));

        Assert.True(decision.CanAttend);
        Assert.True(decision.CanVote);
        Assert.Equal(AssemblyGovernanceCodes.EligibilityStatus.Enabled, decision.Status);
    }

    [Fact]
    public void Lodge_debt_disables_vote_but_keeps_attendance()
    {
        var decision = AssemblyEligibilityPolicy.Evaluate(new AssemblyEligibilityInput(true, false, false, false, true));

        Assert.True(decision.CanAttend);
        Assert.False(decision.CanVote);
        Assert.Contains(AssemblyGovernanceCodes.Reason.LodgeTreasuryOverdue, decision.Reasons);
    }
}
