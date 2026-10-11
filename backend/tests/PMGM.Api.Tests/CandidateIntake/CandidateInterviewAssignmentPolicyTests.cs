using PMGM.Api.Modules.CandidateIntake;
using Xunit;

namespace PMGM.Api.Tests.CandidateIntake;

public sealed class CandidateInterviewAssignmentPolicyTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);
    private static Guid[] Masters() => [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    [Fact]
    public void ThreeDifferentMastersAndValidCouncilAct_AreAccepted()
    {
        Assert.Null(CandidateInterviewAssignmentPolicy.Validate(
            Masters(), CandidateInterviewAssignmentPolicy.Council, Today, Today, "ACTA-2026-010"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void LessThanThreeOrMoreThanSix_AreBlocked(int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        Assert.NotNull(CandidateInterviewAssignmentPolicy.Validate(
            ids, CandidateInterviewAssignmentPolicy.Chamber, Today, Today, "ACTA-2026-010"));
    }

    [Fact]
    public void SameMasterCannotBeDesignatedTwice()
    {
        var id = Guid.NewGuid();
        Assert.Contains("diferente", CandidateInterviewAssignmentPolicy.Validate(
            [id, id, Guid.NewGuid()], CandidateInterviewAssignmentPolicy.Council,
            Today, Today, "ACTA-2026-010")!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidDecisionBodyOrFictitiousMinute_IsBlocked()
    {
        var ids = Masters();
        Assert.NotNull(CandidateInterviewAssignmentPolicy.Validate(
            ids, "venerable_unilateral", Today, Today, "ACTA-2026-010"));
        Assert.NotNull(CandidateInterviewAssignmentPolicy.Validate(
            ids, CandidateInterviewAssignmentPolicy.Chamber, Today, Today, " "));
    }

    [Fact]
    public void DecisionCannotBeFutureDated()
    {
        Assert.NotNull(CandidateInterviewAssignmentPolicy.Validate(
            Masters(), CandidateInterviewAssignmentPolicy.Council,
            Today.AddDays(1), Today, "ACTA-2026-010"));
    }

    [Fact]
    public void AcceptanceMustPrecedeDeliveryAndNotApplyToReplacements()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(CandidateInterviewAssignmentPolicy.CanAccept("assigned", null));
        Assert.False(CandidateInterviewAssignmentPolicy.CanDeliver("assigned", null));
        Assert.False(CandidateInterviewAssignmentPolicy.CanAccept("assigned", now));
        Assert.True(CandidateInterviewAssignmentPolicy.CanDeliver("assigned", now));
        Assert.False(CandidateInterviewAssignmentPolicy.CanAccept("replaced", null));
        Assert.False(CandidateInterviewAssignmentPolicy.CanDeliver("completed", now));
    }

    [Fact]
    public void AdditionalMastersMayBeRequestedWithoutWeakeningMinimum()
    {
        var ids = Masters().Concat([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]).ToArray();
        Assert.Null(CandidateInterviewAssignmentPolicy.Validate(
            ids, CandidateInterviewAssignmentPolicy.Chamber, Today, Today, "ACTA-2026-010"));
    }
}
