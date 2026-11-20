using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadExistingRecordPolicyTests
{
    [Theory]
    [InlineData(InitialLoadExistingRecordPolicy.Match.Absent, "NEW_RECORD_REQUIRES_TRANSACTION", false, false)]
    [InlineData(InitialLoadExistingRecordPolicy.Match.Exact, "EXACT_EXISTING_RECORD_SKIP", true, false)]
    [InlineData(InitialLoadExistingRecordPolicy.Match.Contradictory, "NATURAL_KEY_CONFLICT", false, true)]
    [InlineData(InitialLoadExistingRecordPolicy.Match.Unresolved, "REFERENCE_UNRESOLVED", false, true)]
    [InlineData(InitialLoadExistingRecordPolicy.Match.Ambiguous, "REFERENCE_AMBIGUOUS", false, true)]
    public void Match_decision_never_authorizes_writes(
        InitialLoadExistingRecordPolicy.Match match, string expected,
        bool skip, bool blocks)
    {
        var decision = InitialLoadExistingRecordPolicy.Inspect(match);
        Assert.Equal(expected, decision.Code);
        Assert.Equal(skip, decision.CanSkip);
        Assert.Equal(blocks, decision.BlocksBatch);
        Assert.False(decision.CanApply);
        Assert.Equal(0, decision.WritesPerformed);
    }

    [Fact]
    public void Any_conflict_blocks_entire_plan_and_unknown_enum_fails_closed()
    {
        Assert.True(InitialLoadExistingRecordPolicy.CanPresentPlan(
            [InitialLoadExistingRecordPolicy.Match.Absent,
             InitialLoadExistingRecordPolicy.Match.Exact]));
        Assert.False(InitialLoadExistingRecordPolicy.CanPresentPlan(
            [InitialLoadExistingRecordPolicy.Match.Exact,
             InitialLoadExistingRecordPolicy.Match.Contradictory]));
        Assert.False(InitialLoadExistingRecordPolicy.CanPresentPlan(null));
        var unknown = InitialLoadExistingRecordPolicy.Inspect(
            (InitialLoadExistingRecordPolicy.Match)999);
        Assert.True(unknown.BlocksBatch);
        Assert.False(unknown.CanApply);
    }
}
