using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Admissions.Entities;
using Xunit;

namespace PMGM.Api.Tests.Admissions;

public sealed class AdmissionWithdrawalEvidencePolicyTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("legacy", false)]
    [InlineData("replacement", false)]
    [InlineData("rejected", false)]
    [InlineData("pending", false)]
    [InlineData("no_version", false)]
    [InlineData("no_date", false)]
    [InlineData("wrong_date", false)]
    [InlineData("wrong_mode", false)]
    [InlineData("future_review", false)]
    [InlineData("before_grant", false)]
    [InlineData("rereviewed", false)]
    [InlineData("later_rejection", false)]
    [InlineData("no_source", false)]
    public void Only_current_accredited_letter_and_bound_review_count(string scenario, bool expected)
    {
        var c = new AdmissionCase { AdmissionType = "affiliation", AffiliationMode = "simple", WithdrawalLetterGrantedDate = new(2026, 10, 1), Status = "under_review", CreatedBySubject = "synthetic", CreatedAtUtc = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero) };
        var letter = new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", DocumentVersionId = Guid.NewGuid(), EvidenceDate = new(2026, 10, 1), ReviewStatus = "approved", ReviewedAtUtc = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero), CreatedBySubject = "synthetic", CreatedAtUtc = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero) };
        c.Evidence.Add(letter);
        var d = new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = AdmissionWorkflowCodes.DecisionType.WithdrawalSignature(letter.Id), Status = "approved", AsOfDate = new(2026, 10, 3), SourceReference = "SYNTHETIC-REVIEW", RecordedBySubject = "synthetic", RecordedAtUtc = new(2026, 10, 3, 13, 0, 0, TimeSpan.Zero) }; c.Decisions.Add(d);
        switch (scenario)
        {
            case "legacy": d.DecisionType = AdmissionWorkflowCodes.DecisionType.WithdrawalLetterHandwrittenSignature; break;
            case "replacement": c.Evidence.Add(new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", ReviewStatus = "pending", CreatedBySubject = "synthetic", CreatedAtUtc = letter.CreatedAtUtc.AddDays(1) }); break;
            case "rejected": letter.ReviewStatus = "rejected"; break;
            case "pending": letter.ReviewStatus = "pending"; break;
            case "no_version": letter.DocumentVersionId = null; break;
            case "no_date": letter.EvidenceDate = null; break;
            case "wrong_date": letter.EvidenceDate = new(2026, 9, 30); break;
            case "wrong_mode": c.AffiliationMode = "activation"; break;
            case "future_review": d.AsOfDate = new(2026, 10, 4); break;
            case "before_grant": d.AsOfDate = new(2026, 9, 30); break;
            case "rereviewed": letter.ReviewedAtUtc = d.RecordedAtUtc.AddSeconds(1); break;
            case "later_rejection": c.Decisions.Add(new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = d.DecisionType, Status = "rejected", AsOfDate = d.AsOfDate, RecordedBySubject = "synthetic", RecordedAtUtc = d.RecordedAtUtc.AddSeconds(1) }); break;
            case "no_source": d.SourceReference = null; break;
        }
        Assert.Equal(expected, AdmissionWithdrawalEvidencePolicy.VerifiedSignature(c, new(2026, 10, 3)) is not null);
        if (!expected) Assert.False(AdmissionCaseEligibilityProjector.Evaluate(c).Decision.CanProceed);
    }
}
