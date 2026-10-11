using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class CeremonyInitiationBallotEligibilityPolicyTests
{
    [Theory]
    [InlineData(null, CeremonyCodes.ValidationStatus.Observed, false)]
    [InlineData(CeremonyCodes.ValidationStatus.Pending, CeremonyCodes.ValidationStatus.Observed, false)]
    [InlineData(CeremonyCodes.ValidationStatus.Observed, CeremonyCodes.ValidationStatus.Observed, false)]
    [InlineData(CeremonyCodes.ValidationStatus.Rejected, CeremonyCodes.ValidationStatus.Rejected, false)]
    [InlineData(CeremonyCodes.ValidationStatus.ExceptionApproved, CeremonyCodes.ValidationStatus.Observed, false)]
    [InlineData(CeremonyCodes.ValidationStatus.Approved, CeremonyCodes.ValidationStatus.Approved, true)]
    public void InitiationHasExactlyOneBallotRequirementAndAgreesWithAuthorizationGuard(
        string? lastBallotStatus,
        string expectedRequirementStatus,
        bool canAuthorize)
    {
        var decision = CeremonyEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Initiation,
            CeremonyCodes.ValidationStatus.Approved,
            TreasuryCodes.RegularityStatus.UpToDate,
            HospitalariaCodes.RegularityStatus.UpToDate,
            CeremonyCodes.ValidationStatus.Approved,
            new CandidatePublicationEvidence(Guid.NewGuid(), CeremonyCodes.PublicationStatus.Published,
                20, 21, CeremonyCodes.Rules.InitiationPublicationMinimumDays),
            ceremonyRightPaid: true,
            candidateFinalBallotStatus: lastBallotStatus);

        var ballot = Assert.Single(decision.Requirements,
            requirement => requirement.Code == CeremonyCodes.ValidationType.CandidateFinalBallot);
        Assert.Equal(expectedRequirementStatus, ballot.Status);
        Assert.Equal(canAuthorize, decision.CanAuthorize);
    }

    [Fact]
    public void OtherCeremoniesDoNotGainAnInitiationBallotRequirement()
    {
        var decision = CeremonyEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Affiliation,
            CeremonyCodes.ValidationStatus.Approved,
            TreasuryCodes.RegularityStatus.UpToDate,
            HospitalariaCodes.RegularityStatus.UpToDate,
            CeremonyCodes.ValidationStatus.Approved,
            publication: null,
            ceremonyRightPaid: true);

        Assert.DoesNotContain(decision.Requirements,
            requirement => requirement.Code == CeremonyCodes.ValidationType.CandidateFinalBallot);
    }
}
