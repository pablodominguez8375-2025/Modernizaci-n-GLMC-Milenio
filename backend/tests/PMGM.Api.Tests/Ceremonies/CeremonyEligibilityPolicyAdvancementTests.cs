using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

/// <summary>
/// La misma política de autorización es usada por GetEligibility y por POST /autorizar.
/// En esas rutas no se proveen aún umbrales ni evidencias de avance: no pueden
/// otorgar autorizaciones positivas sólo por reunir los vistos buenos.
/// </summary>
public sealed class CeremonyEligibilityPolicyAdvancementTests
{
    [Theory]
    [InlineData(CeremonyCodes.Type.WageIncrease)]
    [InlineData(CeremonyCodes.Type.Exaltation)]
    public void ActualAuthorizationPolicy_BlocksAdvancementWithoutEvidence(string ceremonyType)
    {
        var result = Evaluate(ceremonyType);

        Assert.False(result.CanAuthorize);
        Assert.Equal("observed", result.Status);
        Assert.Contains(result.Requirements, x =>
            x.Code == CeremonyCodes.ValidationType.AdvancementEligibility &&
            x.Status == CeremonyCodes.ValidationStatus.Observed);
    }

    [Fact]
    public void Initiation_DoesNotRequireAdvancementEvidence()
    {
        var result = CeremonyEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Initiation,
            CeremonyCodes.ValidationStatus.Approved,
            TreasuryCodes.RegularityStatus.UpToDate,
            HospitalariaCodes.RegularityStatus.UpToDate,
            CeremonyCodes.ValidationStatus.Approved,
            new CandidatePublicationEvidence(Guid.NewGuid(), CeremonyCodes.PublicationStatus.Completed, 20, 20, "pub-v1"),
            ceremonyRightPaid: true);

        Assert.True(result.CanAuthorize);
        Assert.DoesNotContain(result.Requirements,
            x => x.Code == CeremonyCodes.ValidationType.AdvancementEligibility);
    }

    [Theory]
    [InlineData(CeremonyCodes.Type.WageIncrease, 1)]
    [InlineData(CeremonyCodes.Type.Exaltation, 2)]
    public void VersionedEvidence_AuthorizesAdvancementAfterOrdinaryCompliance(string ceremonyType, int degree)
    {
        var decision = AdvancementEligibilityPolicy.Evaluate(
            ceremonyType,
            new AdvancementThresholds(4, 2, 1, "valid-rule-v1"),
            new AdvancementEvidence(4, 2, 1));

        var result = Evaluate(ceremonyType, decision);

        Assert.Equal(degree, decision.SourceDegree);
        Assert.True(result.CanAuthorize);
        Assert.Contains(result.Requirements, x =>
            x.Code == CeremonyCodes.ValidationType.AdvancementEligibility &&
            x.Status == CeremonyCodes.ValidationStatus.Approved);
    }

    [Fact]
    public void FalseDegreeEvidence_DoesNotAuthorizeWrongTransition()
    {
        var evidenceForExaltation = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Exaltation,
            new AdvancementThresholds(4, 2, 1, "valid-rule-v1"),
            new AdvancementEvidence(4, 2, 1));

        var result = Evaluate(CeremonyCodes.Type.WageIncrease, evidenceForExaltation);

        Assert.False(result.CanAuthorize);
        Assert.Contains(result.Requirements, x =>
            x.Code == CeremonyCodes.ValidationType.AdvancementEligibility &&
            x.Status == CeremonyCodes.ValidationStatus.Observed);
    }

    [Fact]
    public void MissingRuleVersion_IsInsufficientEvenWithCompletedCounts()
    {
        var decision = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Exaltation,
            new AdvancementThresholds(1, 1, 1, "  "),
            new AdvancementEvidence(1, 1, 1));

        Assert.False(Evaluate(CeremonyCodes.Type.Exaltation, decision).CanAuthorize);
    }

    [Fact]
    public void DocumentaryDispensation_IsNotEnoughWithoutCouncilOrResolutionEvidence()
    {
        var decision = AdvancementEligibilityPolicy.Evaluate(
            CeremonyCodes.Type.Exaltation,
            new AdvancementThresholds(5, 3, 2, "valid-rule-v2"),
            new AdvancementEvidence(3, 1, 0),
            new DispensationEvidence(true, "ACTA-001", CeremonyCodes.ValidationStatus.ExceptionApproved, null));

        Assert.False(Evaluate(CeremonyCodes.Type.Exaltation, decision).CanAuthorize);
        Assert.Equal(AdvancementDispensationStatuses.PendingInternalAffairs, decision.Dispensation?.Status);
    }

    private static CeremonyEligibilityDecision Evaluate(
        string type,
        AdvancementEligibilityDecision? advancement = null)
        => CeremonyEligibilityPolicy.Evaluate(
            type,
            CeremonyCodes.ValidationStatus.Approved,
            TreasuryCodes.RegularityStatus.UpToDate,
            HospitalariaCodes.RegularityStatus.UpToDate,
            CeremonyCodes.ValidationStatus.Approved,
            null,
            ceremonyRightPaid: true,
            advancement: advancement);
}
