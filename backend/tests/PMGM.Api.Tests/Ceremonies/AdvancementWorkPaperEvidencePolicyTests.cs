using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementWorkPaperEvidencePolicyTests
{
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _organization = Guid.NewGuid();
    private static readonly DateOnly Start = new(2024, 9, 1);
    private static readonly DateOnly Cutoff = new(2026, 10, 9);

    [Fact]
    public void TwoSeparateKindsAndDocuments_AreOnlyReadyForFormalReview()
    {
        var symbolic = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var general = Candidate(AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture);
        var result = Review([symbolic, general]);

        Assert.True(result.HasTwoDifferentWorkTypesWithReferences);
        Assert.Equal(1, result.SymbolismCandidates);
        Assert.Equal(1, result.GeneralCultureCandidates);
        Assert.False(result.InstitutionallyAccredited);
        Assert.False(result.AuthorizesCeremony);
    }

    [Fact]
    public void RepeatedVersionOrDuplicateEntries_CannotSubstituteSecondKind()
    {
        var one = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var otherVersion = one with { DocumentVersionId = Guid.NewGuid() };
        var result = Review([one, one, otherVersion]);

        Assert.Equal(1, result.SymbolismCandidates);
        Assert.Equal(0, result.GeneralCultureCandidates);
        Assert.False(result.HasTwoDifferentWorkTypesWithReferences);
    }

    [Fact]
    public void SameDocumentCategorizedTwice_IsNotTwoWorks()
    {
        var one = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var relabeled = one with { WorkKind = AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture };
        Assert.False(Review([one, relabeled]).HasTwoDifferentWorkTypesWithReferences);
    }

    [Fact]
    public void ContradictoryClassificationOfOneDocument_IsExcludedEvenWithThirdWork()
    {
        var reused = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var disguised = reused with
        {
            DocumentVersionId = Guid.NewGuid(),
            WorkKind = AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture
        };
        var third = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var review = Review([reused, disguised, third]);

        Assert.Equal(1, review.SymbolismCandidates);
        Assert.Equal(0, review.GeneralCultureCandidates);
        Assert.False(review.HasTwoDifferentWorkTypesWithReferences);
    }

    [Theory]
    [InlineData("miscellaneous")]
    [InlineData("")]
    public void UnrecognizedWorkKind_DoesNotCount(string kind)
    {
        var valid = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        Assert.False(Review([valid, Candidate(kind)]).HasTwoDifferentWorkTypesWithReferences);
    }

    [Fact]
    public void WrongMemberLodgeDegreeAndOutsideDates_AreRejected()
    {
        var valid = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var general = Candidate(AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture);
        var wrong = new[]
        {
            general with { AuthorMemberId = Guid.NewGuid() },
            general with { OrganizationId = Guid.NewGuid() },
            general with { AuthorDegree = 2 },
            general with { PresentationDate = Start },
            general with { PresentationDate = Cutoff.AddDays(1) }
        };
        Assert.All(wrong, candidate =>
        {
            var result = Review([valid, candidate]);
            Assert.False(result.HasTwoDifferentWorkTypesWithReferences);
            Assert.Equal(0, result.GeneralCultureCandidates);
        });
    }

    [Fact]
    public void MissingFormalEvidenceReferences_AlwaysBlocksReadiness()
    {
        var symbolic = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var validGeneral = Candidate(AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture);
        var incomplete = new[]
        {
            validGeneral with { DocumentVersionId = Guid.Empty },
            validGeneral with { HeldMeetingId = Guid.Empty },
            validGeneral with { PresentationMinutesVersionId = Guid.Empty },
            validGeneral with { CouncilApprovalMinutesVersionId = Guid.Empty }
        };
        Assert.All(incomplete, candidate =>
            Assert.False(Review([symbolic, candidate]).HasTwoDifferentWorkTypesWithReferences));
    }

    [Fact]
    public void InvalidInstitutionalScope_AlwaysFailsClosed()
    {
        var candidate = Candidate(AdvancementWorkPaperEvidencePolicy.DegreeSymbolism);
        var general = Candidate(AdvancementWorkPaperEvidencePolicy.MasonicGeneralCulture);
        var review = AdvancementWorkPaperEvidencePolicy.Review(
            Guid.Empty, _organization, 1, Start, Cutoff, [candidate, general]);
        Assert.False(review.HasTwoDifferentWorkTypesWithReferences);
        Assert.False(review.AuthorizesCeremony);
    }

    private WorkPaperEvidenceReadiness Review(IEnumerable<WorkPaperAttestationCandidate> records)
        => AdvancementWorkPaperEvidencePolicy.Review(_member, _organization, 1, Start, Cutoff, records);

    private WorkPaperAttestationCandidate Candidate(string kind) =>
        new(Guid.NewGuid(), Guid.NewGuid(), _member, _organization, 1, kind,
            new DateOnly(2026, 8, 15), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
