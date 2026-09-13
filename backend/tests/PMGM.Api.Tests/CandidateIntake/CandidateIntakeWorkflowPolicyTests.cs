using PMGM.Api.Modules.CandidateIntake;
using Xunit;

namespace PMGM.Api.Tests.CandidateIntake;

public sealed class CandidateIntakeWorkflowPolicyTests
{
    [Fact]
    public void InitialDeliberation_IsBlocked_BeforeSevenDays()
    {
        var decision = CandidateIntakeWorkflowPolicy.EvaluateInitialDeliberation(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 7),
            presentVoters: 8,
            votesInFavor: 8);

        Assert.False(decision.CanProceed);
        Assert.False(decision.IsRejected);
        Assert.Equal("initial_deliberation.waiting_period", decision.Code);
    }

    [Fact]
    public void InitialDeliberation_Allows_ExactlyAtSevenDays_WhenUnanimous()
    {
        var decision = CandidateIntakeWorkflowPolicy.EvaluateInitialDeliberation(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 8),
            presentVoters: 8,
            votesInFavor: 8);

        Assert.True(decision.CanProceed);
    }

    [Fact]
    public void InitialDeliberation_Rejects_WhenVoteIsNotUnanimous()
    {
        var decision = CandidateIntakeWorkflowPolicy.EvaluateInitialDeliberation(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 8),
            presentVoters: 8,
            votesInFavor: 7);

        Assert.False(decision.CanProceed);
        Assert.True(decision.IsRejected);
        Assert.Equal("initial_deliberation.unanimity", decision.Code);
    }

    [Fact]
    public void ThirdDegreePackage_RequiresAtLeastThreeInterviews_Questionnaire_AndAutobiography()
    {
        var missingInterview = CandidateIntakeWorkflowPolicy.EvaluateInterviewPackage(2, true, true);
        var missingQuestionnaire = CandidateIntakeWorkflowPolicy.EvaluateInterviewPackage(3, false, true);
        var missingAutobiography = CandidateIntakeWorkflowPolicy.EvaluateInterviewPackage(3, true, false);
        var additionalInterview = CandidateIntakeWorkflowPolicy.EvaluateInterviewPackage(4, true, true);
        var complete = CandidateIntakeWorkflowPolicy.EvaluateInterviewPackage(3, true, true);

        Assert.False(missingInterview.CanProceed);
        Assert.False(missingQuestionnaire.CanProceed);
        Assert.False(missingAutobiography.CanProceed);
        Assert.True(additionalInterview.CanProceed);
        Assert.True(complete.CanProceed);
    }

    [Fact]
    public void ThirdDegreeOpenVote_RequiresConsistentAggregateTotals()
    {
        var invalid = CandidateIntakeWorkflowPolicy.EvaluateThirdDegreeOpenVote(12, 10, 1, 0, approved: true);
        var favorable = CandidateIntakeWorkflowPolicy.EvaluateThirdDegreeOpenVote(12, 10, 2, 0, approved: true);
        var unfavorable = CandidateIntakeWorkflowPolicy.EvaluateThirdDegreeOpenVote(12, 5, 7, 0, approved: false);

        Assert.False(invalid.CanProceed);
        Assert.Equal("third_degree_review.vote_totals", invalid.Code);
        Assert.True(favorable.CanProceed);
        Assert.True(unfavorable.IsRejected);
    }

    [Fact]
    public void FinalBallot_IsBlocked_BeforeTwentyPublicationDays()
    {
        var decision = CandidateIntakeWorkflowPolicy.EvaluateFinalBallot(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 20),
            thirdDegreeOpenVoteApproved: true,
            minimumPublicationDays: 20);

        Assert.False(decision.CanProceed);
        Assert.Equal("first_degree_ballot.publication_period", decision.Code);
    }

    [Fact]
    public void FinalBallot_IsAllowed_ExactlyAtTwentyPublicationDays()
    {
        var decision = CandidateIntakeWorkflowPolicy.EvaluateFinalBallot(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 21),
            thirdDegreeOpenVoteApproved: true,
            minimumPublicationDays: 20);

        Assert.True(decision.CanProceed);
    }

    [Fact]
    public void FinalBallot_IsBlocked_WhenThirdDegreeVoteIsNotApproved()
    {
        var decision = CandidateIntakeWorkflowPolicy.EvaluateFinalBallot(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 25),
            thirdDegreeOpenVoteApproved: false,
            minimumPublicationDays: 20);

        Assert.False(decision.CanProceed);
        Assert.Equal("first_degree_ballot.third_degree_review", decision.Code);
    }

    [Fact]
    public void FinalBallotRounds_RequireUniqueProcedures_AndConsistentCounts()
    {
        var invalidCount = CandidateIntakeWorkflowPolicy.EvaluateFinalBallotRounds(
            [new CandidateBallotRound(1, 12, 10, 1)], approved: true);
        var repeatedProcedure = CandidateIntakeWorkflowPolicy.EvaluateFinalBallotRounds(
            [new CandidateBallotRound(1, 12, 11, 1), new CandidateBallotRound(1, 12, 12, 0)], approved: true);
        var approved = CandidateIntakeWorkflowPolicy.EvaluateFinalBallotRounds(
            [new CandidateBallotRound(1, 12, 11, 1), new CandidateBallotRound(2, 12, 12, 0)], approved: true);

        Assert.Equal("first_degree_ballot.counts", invalidCount.Code);
        Assert.Equal("first_degree_ballot.procedures", repeatedProcedure.Code);
        Assert.True(approved.CanProceed);
    }

    [Fact]
    public void InitiationRequest_ReusesApprovedCase_AndRequiresBothWorkshopRoles()
    {
        var invalidDate = CandidateIntakeWorkflowPolicy.EvaluateInitiationRequestSubmission(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 11), true, "Secretaría");
        var missingVenerable = CandidateIntakeWorkflowPolicy.EvaluateInitiationRequestSubmission(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 20), false, "Secretaría");
        var missingSecretary = CandidateIntakeWorkflowPolicy.EvaluateInitiationRequestSubmission(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 20), true, "");
        var complete = CandidateIntakeWorkflowPolicy.EvaluateInitiationRequestSubmission(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 20), true, "Secretaria Demostrativa");

        Assert.Equal("initiation_request.proposed_date", invalidDate.Code);
        Assert.Equal("initiation_request.venerable_approval", missingVenerable.Code);
        Assert.Equal("initiation_request.secretary", missingSecretary.Code);
        Assert.True(complete.CanProceed);
    }

    [Fact]
    public void RePresentation_RequiresOneYear_AndRemediedCauses()
    {
        var rejectionDate = new DateOnly(2025, 9, 10);

        var tooEarly = CandidateIntakeWorkflowPolicy.EvaluateRePresentation(
            rejectionDate,
            new DateOnly(2026, 9, 9),
            rejectionCausesRemedied: true);
        var notRemedied = CandidateIntakeWorkflowPolicy.EvaluateRePresentation(
            rejectionDate,
            new DateOnly(2026, 9, 10),
            rejectionCausesRemedied: false);
        var allowed = CandidateIntakeWorkflowPolicy.EvaluateRePresentation(
            rejectionDate,
            new DateOnly(2026, 9, 10),
            rejectionCausesRemedied: true);

        Assert.False(tooEarly.CanProceed);
        Assert.False(notRemedied.CanProceed);
        Assert.True(allowed.CanProceed);
    }
}
