using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.CandidateIntake;

public sealed class CandidateRePresentationReviewPolicyTests
{
    [Theory]
    [InlineData(CeremonyCodes.ValidationStatus.Observed, CeremonyCodes.ValidationStatus.Observed, false)]
    [InlineData(CeremonyCodes.ValidationStatus.Pending, CeremonyCodes.ValidationStatus.Observed, false)]
    [InlineData(CeremonyCodes.ValidationStatus.Rejected, CeremonyCodes.ValidationStatus.Rejected, false)]
    [InlineData(CeremonyCodes.ValidationStatus.ExceptionApproved, CeremonyCodes.ValidationStatus.Observed, false)]
    [InlineData(CeremonyCodes.ValidationStatus.Approved, CeremonyCodes.ValidationStatus.Approved, true)]
    public void LatestInteriorReviewGatesInitiation(string status, string expected, bool canAuthorize)
    {
        var result = CeremonyEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Initiation, CeremonyCodes.ValidationStatus.Approved,
            TreasuryCodes.RegularityStatus.UpToDate, HospitalariaCodes.RegularityStatus.UpToDate,
            CeremonyCodes.ValidationStatus.Approved,
            new CandidatePublicationEvidence(Guid.NewGuid(), CeremonyCodes.PublicationStatus.Published,
                20, 21, CeremonyCodes.Rules.InitiationPublicationMinimumDays),
            candidateFinalBallotStatus: CeremonyCodes.ValidationStatus.Approved,
            candidateRePresentationReviewStatus: status);
        var review = Assert.Single(result.Requirements, x =>
            x.Code == CeremonyCodes.ValidationType.CandidateRepresentationReview);
        Assert.Equal(expected, review.Status);
        Assert.Equal(canAuthorize, result.CanAuthorize);
    }

    [Fact]
    public void FirstTimeInitiationDoesNotRequireRePresentationReview()
    {
        var result = CeremonyEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Initiation, CeremonyCodes.ValidationStatus.Approved,
            TreasuryCodes.RegularityStatus.UpToDate, HospitalariaCodes.RegularityStatus.UpToDate,
            CeremonyCodes.ValidationStatus.Approved,
            new CandidatePublicationEvidence(Guid.NewGuid(), CeremonyCodes.PublicationStatus.Published,
                20, 21, CeremonyCodes.Rules.InitiationPublicationMinimumDays),
            candidateFinalBallotStatus: CeremonyCodes.ValidationStatus.Approved);
        Assert.True(result.CanAuthorize);
        Assert.DoesNotContain(result.Requirements,
            x => x.Code == CeremonyCodes.ValidationType.CandidateRepresentationReview);
    }

    [Theory]
    [InlineData(CeremonyCodes.ValidationStatus.Approved)]
    [InlineData(CeremonyCodes.ValidationStatus.Observed)]
    [InlineData(CeremonyCodes.ValidationStatus.Rejected)]
    public void FormalReviewRequiresReference(string status)
    {
        Assert.False(CandidateRePresentationReviewPolicy.CanResolve(status, null, null));
        Assert.False(CandidateRePresentationReviewPolicy.CanResolve(status, " ", null));
        Assert.False(CandidateRePresentationReviewPolicy.CanResolve(status, new string('x', 241), null));
        Assert.True(CandidateRePresentationReviewPolicy.CanResolve(status, "ACTA-RI-001", null));
    }

    [Theory]
    [InlineData(CeremonyCodes.ValidationStatus.ExceptionApproved)]
    [InlineData(CeremonyCodes.ValidationStatus.NotApplicable)]
    [InlineData(CeremonyCodes.ValidationStatus.Pending)]
    public void OtherStatusesCannotResolve(string status)
        => Assert.False(CandidateRePresentationReviewPolicy.CanResolve(status, "ACTA-RI-001", null));
}
