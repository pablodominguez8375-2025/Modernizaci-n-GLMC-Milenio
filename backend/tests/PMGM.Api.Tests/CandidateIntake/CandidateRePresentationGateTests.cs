using PMGM.Api.Modules.CandidateIntake;
using Xunit;

namespace PMGM.Api.Tests.CandidateIntake;

public sealed class CandidateRePresentationGateTests
{
    private static readonly Guid PriorValidationId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid PriorRequestId = Guid.Parse("22222222-3333-4444-8555-666666666666");

    [Fact]
    public void FirstTimeCandidate_DoesNotRequireRePresentationEvidence()
    {
        var result = CandidateRePresentationGate.Evaluate(null, new DateOnly(2026, 10, 11), false, null);
        Assert.True(result.CanProceed);
        Assert.False(result.IsRePresentation);
    }

    [Fact]
    public void FirstTimeCandidate_CannotInventARejectionClearance()
    {
        var result = CandidateRePresentationGate.Evaluate(null, new DateOnly(2026, 10, 11), true, "ACTA-001");
        Assert.False(result.CanProceed);
        Assert.Equal("representation.no_prior_rejection", result.Code);
    }

    [Theory]
    [InlineData(2026, 9, 29)]
    [InlineData(2026, 9, 28)]
    public void RejectedCandidate_BlockedBeforeFullYear(int year, int month, int day)
    {
        var result = CandidateRePresentationGate.Evaluate(
            Prior(new DateOnly(2025, 9, 30)), new DateOnly(year, month, day), true, "ACTA-RI-001");
        Assert.False(result.CanProceed);
        Assert.Equal("representation.minimum_year", result.Code);
    }

    [Fact]
    public void RejectedCandidate_BlockedWithoutCausesRemediedEvenAfterYear()
    {
        var result = CandidateRePresentationGate.Evaluate(
            Prior(new DateOnly(2025, 9, 30)), new DateOnly(2026, 10, 11), false, "ACTA-RI-001");
        Assert.False(result.CanProceed);
        Assert.Equal("representation.causes_remedied", result.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void RejectedCandidate_BlockedWithoutEvidenceReference(string? reference)
    {
        var result = CandidateRePresentationGate.Evaluate(
            Prior(new DateOnly(2025, 9, 30)), new DateOnly(2026, 10, 11), true, reference);
        Assert.False(result.CanProceed);
        Assert.Equal("representation.evidence_required", result.Code);
    }

    [Fact]
    public void RejectedCandidate_BlockedForOverlongReference()
    {
        var result = CandidateRePresentationGate.Evaluate(
            Prior(new DateOnly(2025, 9, 30)), new DateOnly(2026, 10, 11), true, new string('A', 241));
        Assert.False(result.CanProceed);
        Assert.Equal("representation.evidence_required", result.Code);
    }

    [Fact]
    public void RejectedCandidate_AllowedOnExactAnniversaryWithSource()
    {
        var result = CandidateRePresentationGate.Evaluate(
            Prior(new DateOnly(2025, 9, 30)), new DateOnly(2026, 9, 30), true, " ACTA-RI-001 ");
        Assert.True(result.CanProceed);
        Assert.True(result.IsRePresentation);
        Assert.Equal(PriorValidationId, result.PriorRejection?.ValidationId);
        Assert.Equal("ACTA-RI-001", result.SourceReference);
    }

    [Fact]
    public void LeapYearRejection_UsesCalendarYear()
    {
        var result = CandidateRePresentationGate.Evaluate(
            Prior(new DateOnly(2024, 2, 29)), new DateOnly(2025, 2, 28), true, "ACTA-LEAP");
        Assert.True(result.CanProceed);
    }

    [Fact]
    public void RePresentationProfileDates_CannotBeBackdatedBeforeAnniversary()
    {
        var prior = Prior(new DateOnly(2025, 9, 30));
        Assert.NotNull(CandidateRePresentationGate.ValidateRePresentationProfileDates(
            prior, new DateOnly(2026, 9, 29), null));
        Assert.NotNull(CandidateRePresentationGate.ValidateRePresentationProfileDates(
            prior, new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 29)));
        Assert.Null(CandidateRePresentationGate.ValidateRePresentationProfileDates(
            prior, new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 1)));
        Assert.Null(CandidateRePresentationGate.ValidateRePresentationProfileDates(
            null, new DateOnly(2025, 9, 1), null));
    }

    private static CandidateRePresentationGate.PriorRejection Prior(DateOnly date)
        => new(PriorValidationId, PriorRequestId, date);
}
