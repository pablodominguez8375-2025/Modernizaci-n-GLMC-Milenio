using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Admissions.Entities;
using Xunit;
namespace PMGM.Api.Tests.Admissions;
public sealed class AdmissionWithdrawalDateCorrectionTests
{
    [Theory]
    [InlineData("valid", "simple")]
    [InlineData("boundary", "simple")]
    [InlineData("older", "activation")]
    [InlineData("resolved", null)]
    [InlineData("eligible", null)]
    [InlineData("incorporation", null)]
    [InlineData("procedure", null)]
    [InlineData("replacement", null)]
    [InlineData("pending", null)]
    [InlineData("no_date", null)]
    [InlineData("no_version", null)]
    [InlineData("no_review", null)]
    [InlineData("after_creation", null)]
    public void Correction_requires_current_reviewed_letter_and_preserves_creation_date_rule(string scenario, string? expected)
    {
        var today = new DateOnly(2026, 10, 3);
        var c = new AdmissionCase { AdmissionType = "affiliation", AffiliationMode = "simple", Status = "under_review", CreatedBySubject = "synthetic", CreatedAtUtc = new DateTimeOffset(2026, 10, 3, 1, 0, 0, TimeSpan.Zero) };
        var e = new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", EvidenceDate = new DateOnly(2026, 10, 1), DocumentVersionId = Guid.NewGuid(), ReviewStatus = "approved", ReviewedAtUtc = c.CreatedAtUtc, CreatedBySubject = "synthetic" }; c.Evidence.Add(e);
        if (scenario == "boundary") e.EvidenceDate = new DateOnly(2026, 7, 2); // Chile creation date = Oct 2.
        if (scenario == "older") e.EvidenceDate = new DateOnly(2026, 7, 1);
        if (scenario is "resolved" or "eligible") c.Status = scenario;
        if (scenario == "incorporation") c.AdmissionType = scenario;
        if (scenario == "procedure") c.Decisions.Add(new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = "lodge_third_degree_approval", Status = "observed", RecordedBySubject = "synthetic" });
        if (scenario == "replacement") c.Evidence.Add(new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", ReviewStatus = "rejected", CreatedAtUtc = e.CreatedAtUtc.AddSeconds(1), CreatedBySubject = "synthetic" });
        if (scenario == "pending") e.ReviewStatus = "pending";
        if (scenario == "no_date") e.EvidenceDate = null;
        if (scenario == "no_version") e.DocumentVersionId = null;
        if (scenario == "no_review") e.ReviewedAtUtc = null;
        if (scenario == "after_creation") e.EvidenceDate = today;
        Assert.Equal(expected, AdmissionWithdrawalDateCorrection.CorrectedMode(c, e.Id, today));
    }
    [Fact]
    public void Correction_requires_new_signature_even_when_old_approval_matches_document()
    {
        var today = new DateOnly(2026, 10, 3); var now = DateTimeOffset.UtcNow;
        var c = new AdmissionCase { AdmissionType = "affiliation", AffiliationMode = "simple", WithdrawalLetterGrantedDate = today, Status = "under_review", CreatedBySubject = "synthetic", CreatedAtUtc = new DateTimeOffset(2026, 10, 3, 15, 0, 0, TimeSpan.Zero) };
        var e = new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", EvidenceDate = today, DocumentVersionId = Guid.NewGuid(), ReviewStatus = "approved", ReviewedAtUtc = now.AddMinutes(-2), CreatedBySubject = "synthetic" }; c.Evidence.Add(e);
        var d = new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = AdmissionWorkflowCodes.DecisionType.WithdrawalSignature(e.Id), Status = "approved", AsOfDate = today, SourceReference = "SYNTHETIC", RecordedBySubject = "synthetic", RecordedAtUtc = now.AddMinutes(-1) }; c.Decisions.Add(d);
        Assert.NotNull(AdmissionWithdrawalEvidencePolicy.VerifiedSignature(c, today));
        c.Decisions.Add(new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = AdmissionWithdrawalDateCorrection.DecisionPrefix + e.Id, Status = "approved", AsOfDate = today, RecordedBySubject = "synthetic", RecordedAtUtc = now });
        Assert.Null(AdmissionWithdrawalEvidencePolicy.VerifiedSignature(c, today));
        c.Decisions.Add(new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = d.DecisionType, Status = d.Status, AsOfDate = today, SourceReference = d.SourceReference, RecordedBySubject = "synthetic", RecordedAtUtc = now.AddSeconds(1) });
        Assert.NotNull(AdmissionWithdrawalEvidencePolicy.VerifiedSignature(c, today));
    }
}
