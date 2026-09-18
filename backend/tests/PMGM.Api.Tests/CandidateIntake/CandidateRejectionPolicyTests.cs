using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.CandidateIntake;

public sealed class CandidateRejectionPolicyTests
{
    [Theory]
    [InlineData(CeremonyCodes.ValidationType.CandidateThirdDegreeReview)]
    [InlineData(CeremonyCodes.ValidationType.CandidateFinalBallot)]
    public void Rejected_ThirdDegreeOrFinalBallot_BlocksOrderRePresentation(string validationType)
    {
        Assert.True(CandidateRejectionPolicy.IsOrderBlocking(
            validationType,
            CeremonyCodes.ValidationStatus.Rejected));
    }

    [Fact]
    public void ObservedOrOtherStages_DoNotCreateOrderBlockingRejection()
    {
        Assert.False(CandidateRejectionPolicy.IsOrderBlocking(
            CeremonyCodes.ValidationType.CandidateFinalBallot,
            CeremonyCodes.ValidationStatus.Observed));
        Assert.False(CandidateRejectionPolicy.IsOrderBlocking(
            CeremonyCodes.ValidationType.CandidateInitialDeliberation,
            CeremonyCodes.ValidationStatus.Rejected));
    }

    [Fact]
    public void Reasons_DistinguishThirdDegreeFromFirstDegreeBallot()
    {
        Assert.Contains("3.er grado", CandidateRejectionPolicy.GetReason(
            CeremonyCodes.ValidationType.CandidateThirdDegreeReview));
        Assert.Contains("1.er grado", CandidateRejectionPolicy.GetReason(
            CeremonyCodes.ValidationType.CandidateFinalBallot));
    }
}
