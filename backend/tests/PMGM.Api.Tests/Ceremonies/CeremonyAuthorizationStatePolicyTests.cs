using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class CeremonyAuthorizationStatePolicyTests
{
    [Theory]
    [InlineData(CeremonyCodes.RequestStatus.Draft)]
    [InlineData(CeremonyCodes.RequestStatus.UnderReview)]
    [InlineData(CeremonyCodes.RequestStatus.Eligible)]
    [InlineData(CeremonyCodes.RequestStatus.Observed)]
    public void OpenStatusesAllowEvaluationAndStayCorrectable(string status)
    {
        Assert.True(CeremonyAuthorizationStatePolicy.CanAttemptAuthorization(status));
        Assert.Null(CeremonyAuthorizationStatePolicy.EvaluateRequirement(status));
        Assert.Equal(CeremonyCodes.RequestStatus.Observed,
            CeremonyAuthorizationStatePolicy.AfterUnmetRequirements(status));
    }

    [Theory]
    [InlineData(CeremonyCodes.RequestStatus.Authorized)]
    [InlineData(CeremonyCodes.RequestStatus.Completed)]
    [InlineData(CeremonyCodes.RequestStatus.Rejected)]
    [InlineData("unrecognized")]
    public void ClosedAndUnknownStatusesAreNotEligibleAndCannotBeOverwritten(string status)
    {
        Assert.False(CeremonyAuthorizationStatePolicy.CanAttemptAuthorization(status));
        Assert.Equal(status, CeremonyAuthorizationStatePolicy.AfterUnmetRequirements(status));
        var result = CeremonyAuthorizationStatePolicy.EvaluateRequirement(status);
        Assert.NotNull(result);
        Assert.Equal(CeremonyCodes.ValidationStatus.Rejected, result.Status);
    }

    [Theory]
    [InlineData(CeremonyCodes.Type.Initiation)]
    [InlineData(CeremonyCodes.Type.Affiliation)]
    [InlineData(CeremonyCodes.Type.Incorporation)]
    [InlineData(CeremonyCodes.Type.WageIncrease)]
    [InlineData(CeremonyCodes.Type.Exaltation)]
    public void AllCeremonyTypesBlockReauthorizationOfClosedRequests(string type)
    {
        foreach (var closed in new [] {
            CeremonyCodes.RequestStatus.Authorized,
            CeremonyCodes.RequestStatus.Rejected,
            CeremonyCodes.RequestStatus.Completed })
        {
            var decision = CeremonyEligibilityPolicy.Evaluate(
                type, CeremonyCodes.ValidationStatus.Approved,
                TreasuryCodes.RegularityStatus.UpToDate,
                HospitalariaCodes.RegularityStatus.UpToDate,
                CeremonyCodes.ValidationStatus.Approved,
                new CandidatePublicationEvidence(Guid.NewGuid(), CeremonyCodes.PublicationStatus.Completed,
                    20, 30, CeremonyCodes.Rules.InitiationPublicationMinimumDays),
                ceremonyRightPaid: true,
                candidateFinalBallotStatus: CeremonyCodes.ValidationStatus.Approved,
                requestStatus: closed);
            var lifecycle = Assert.Single(decision.Requirements,
                x => x.Code == CeremonyAuthorizationStatePolicy.RequirementCode);
            Assert.Equal(CeremonyCodes.ValidationStatus.Rejected, lifecycle.Status);
            Assert.False(decision.CanAuthorize);
        }
    }

    [Fact]
    public void FirstTimeInitiationWithValidRequirementsAndOpenStatusStillEligible()
    {
        var decision = CeremonyEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Initiation, CeremonyCodes.ValidationStatus.Approved,
            TreasuryCodes.RegularityStatus.UpToDate,
            HospitalariaCodes.RegularityStatus.UpToDate,
            CeremonyCodes.ValidationStatus.Approved,
            new CandidatePublicationEvidence(Guid.NewGuid(), CeremonyCodes.PublicationStatus.Completed,
                20, 30, CeremonyCodes.Rules.InitiationPublicationMinimumDays),
            ceremonyRightPaid: true,
            candidateFinalBallotStatus: CeremonyCodes.ValidationStatus.Approved,
            requestStatus: CeremonyCodes.RequestStatus.UnderReview);
        Assert.True(decision.CanAuthorize);
        Assert.DoesNotContain(decision.Requirements,
            x => x.Code == CeremonyAuthorizationStatePolicy.RequirementCode);
    }
}
