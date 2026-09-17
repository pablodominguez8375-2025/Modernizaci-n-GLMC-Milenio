using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.LodgeManagement;
using Xunit;

namespace PMGM.Api.Tests.LodgeManagement;

public sealed class LodgeCouncilPolicyTests
{
    public static IEnumerable<object[]> CouncilRoles()
    {
        yield return new object[] { InstitutionalRoles.TallerVenerable };
        yield return new object[] { InstitutionalRoles.TallerInmediatoExVenerable };
        yield return new object[] { InstitutionalRoles.TallerPrimerVigilante };
        yield return new object[] { InstitutionalRoles.TallerSegundoVigilante };
        yield return new object[] { InstitutionalRoles.TallerOrador };
        yield return new object[] { InstitutionalRoles.TallerSecretaria };
        yield return new object[] { InstitutionalRoles.TallerTesoreria };
        yield return new object[] { InstitutionalRoles.TallerHospitalaria };
    }

    [Theory]
    [MemberData(nameof(CouncilRoles))]
    public void IsCouncilRole_AcceptsTheEightRegulatoryOffices(string role)
        => Assert.True(LodgeCouncilPolicy.IsCouncilRole(role));

    [Fact]
    public void IsCouncilRole_RejectsTechnicalLodgeAdminProfile()
        => Assert.False(LodgeCouncilPolicy.IsCouncilRole(InstitutionalRoles.TallerAdmin));

    [Fact]
    public void CanVote_GuestNeverVotes()
        => Assert.False(LodgeCouncilPolicy.CanVote(
            LodgeCouncilCodes.ParticipationType.Guest,
            InstitutionalRoles.TallerVenerable));

    [Fact]
    public void CanVote_CouncilMemberCanVote()
        => Assert.True(LodgeCouncilPolicy.CanVote(
            LodgeCouncilCodes.ParticipationType.Member,
            InstitutionalRoles.TallerOrador));

    [Theory]
    [InlineData(LodgeCouncilCodes.DecisionCategory.BudgetProposal)]
    [InlineData(LodgeCouncilCodes.DecisionCategory.InstructionProgramProposal)]
    [InlineData(LodgeCouncilCodes.DecisionCategory.OfficerChangeProposal)]
    [InlineData(LodgeCouncilCodes.DecisionCategory.ForcedWithdrawalProposal)]
    public void RequiresChamberReview_TracksArticle10Point2Proposals(string category)
        => Assert.True(LodgeCouncilPolicy.RequiresChamberReview(category));

    [Theory]
    [InlineData(LodgeCouncilCodes.DecisionCategory.Handover)]
    [InlineData(LodgeCouncilCodes.DecisionCategory.FinancialControl)]
    [InlineData(LodgeCouncilCodes.DecisionCategory.DuesRelief)]
    [InlineData(LodgeCouncilCodes.DecisionCategory.BenevolenceAidProposal)]
    public void RequiresChamberReview_DoesNotInventReferralForCouncilCompetences(string category)
        => Assert.False(LodgeCouncilPolicy.RequiresChamberReview(category));

    [Fact]
    public void ChamberProposal_CannotBeStoredAsFinalCouncilApproval()
        => Assert.False(LodgeCouncilPolicy.IsOutcomeCompatible(
            LodgeCouncilCodes.DecisionCategory.BudgetProposal,
            LodgeCouncilCodes.DecisionOutcome.Approved));

    [Fact]
    public void ChamberProposal_CanBeApprovedForReferral()
        => Assert.True(LodgeCouncilPolicy.IsOutcomeCompatible(
            LodgeCouncilCodes.DecisionCategory.BudgetProposal,
            LodgeCouncilCodes.DecisionOutcome.ApprovedForReferral));

    [Fact]
    public void QualifiedQuorum_RequiresExplicitInstitutionalConfirmation()
    {
        Assert.False(LodgeCouncilPolicy.CanConfirmQualifiedQuorum(false, 8));
        Assert.True(LodgeCouncilPolicy.CanConfirmQualifiedQuorum(true, 1));
    }

    [Fact]
    public void QualifiedQuorum_DoesNotEmbedNumericThresholdFromUnverifiedInterpretation()
        => Assert.True(LodgeCouncilPolicy.CanConfirmQualifiedQuorum(true, 4));
}
