using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Admissions.Entities;
using Xunit;

namespace PMGM.Api.Tests.Admissions;

public sealed class AdmissionResidualEligibilityTests
{
    [Fact]
    public void First_presentation_is_not_a_re_presentation()
    {
        var input = new AdmissionEligibilityInput("affiliation", "simple", true, true, true, true,
            NewPresentationDate: new DateOnly(2026, 10, 3));
        Assert.True(AdmissionEligibilityPolicy.Evaluate(input).CanProceed);
        Assert.DoesNotContain(AdmissionEligibilityPolicy.Evaluate(input).Requirements, x => x.Code == "re_presentation");
    }

    [Theory]
    [InlineData("missing_review")]
    [InlineData("impediment_without_pardon")]
    [InlineData("same_day_ballot")]
    [InlineData("earlier_ballot")]
    [InlineData("missing_source")]
    [InlineData("late_commission")]
    [InlineData("new_appointment")]
    [InlineData("missing_reading")]
    [InlineData("regular_unknown")]
    [InlineData("regular_unrecognized")]
    [InlineData("regular_accepted")]
    [InlineData("regular_known")]
    [InlineData("replaced_legal_doc")]
    [InlineData("valid")]
    [InlineData("pardoned")]
    public void Residual_controls_are_consumed_by_the_common_projection(string scenario)
    {
        var date = AdmissionWithdrawalEvidencePolicy.ChileDate(DateTimeOffset.UtcNow);
        var created = DateTimeOffset.UtcNow.AddDays(-5);
        var c = new AdmissionCase { AdmissionType = "affiliation", AffiliationMode = "activation",
            WithdrawalLetterGrantedDate = date.AddMonths(-4), Status = "under_review", CreatedBySubject = "synthetic", CreatedAtUtc = created };
        var letter = new AdmissionEvidence { EvidenceType = "withdrawal_letter", DocumentVersionId = Guid.NewGuid(),
            EvidenceDate = c.WithdrawalLetterGrantedDate, ReviewStatus = "approved", ReviewedAtUtc = created.AddMinutes(1), CreatedBySubject = "synthetic" };
        c.Evidence.Add(letter);
        void Add(string code, DateOnly when, string status = "approved", DateTimeOffset? recorded = null, string? source = "synthetic")
            => c.Decisions.Add(new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = code, AsOfDate = when,
                Status = status, SourceReference = source, RecordedBySubject = "synthetic", RecordedAtUtc = recorded ?? created.AddHours(1) });
        Add(AdmissionWorkflowCodes.DecisionType.WithdrawalSignature(letter.Id), date.AddDays(-4));
        if (scenario != "missing_review") Add("article_2_3_review", date.AddDays(-4),
            scenario is "impediment_without_pardon" or "pardoned" ? "rejected" : "approved");
        if (scenario == "pardoned") Add("grand_master_pardon", date.AddDays(-3));
        Add("information_commission_appointed", date.AddDays(-4));
        Add("information_commission_completed", scenario == "late_commission" ? date : date.AddDays(-3), recorded: created.AddHours(2));
        if (scenario == "new_appointment") Add("information_commission_appointed", date.AddDays(-2), recorded: created.AddHours(3));
        if (scenario != "missing_reading") Add("lodge_first_degree_presentation", date.AddDays(-4));
        Add("lodge_third_degree_approval", date.AddDays(-2), source: scenario == "missing_source" ? null : "synthetic");
        Add("lodge_first_degree_ballot", scenario == "same_day_ballot" ? date.AddDays(-2) : scenario == "earlier_ballot" ? date.AddDays(-3) : date.AddDays(-1));
        if (scenario.StartsWith("regular_", StringComparison.Ordinal) || scenario == "replaced_legal_doc")
        {
            c.AdmissionType = "incorporation"; c.HasPeaceAndFriendshipPact = true;
            foreach (var code in new[] { "legalized_initiation_evidence", "degree_evidence" })
                c.Evidence.Add(new AdmissionEvidence { EvidenceType = code, ReviewStatus = "approved", DocumentVersionId = Guid.NewGuid(),
                    ReviewedAtUtc = created.AddHours(1), CreatedAtUtc = created, CreatedBySubject = "synthetic" });
            c.Decisions.Single(x => x.DecisionType == "article_2_3_review").Notes = scenario == "regular_unknown" ? null :
                scenario is "regular_known" or "replaced_legal_doc" ? "{\"OriginObedienceRecognized\":true}" : "{\"OriginObedienceRecognized\":false}";
            if (scenario == "regular_accepted") Add("grand_master_regularity_recognition", date.AddDays(-3));
            if (scenario == "replaced_legal_doc") c.Evidence.Add(new AdmissionEvidence { EvidenceType = "legalized_initiation_evidence", ReviewStatus = "pending",
                CreatedAtUtc = created.AddHours(1), CreatedBySubject = "synthetic" });
        }
        Assert.Equal(scenario is "valid" or "pardoned" or "regular_known" or "regular_accepted", AdmissionCaseEligibilityProjector.Evaluate(c).Decision.CanProceed);
    }

    [Fact]
    public void Replay_requires_the_same_effective_date_and_institutional_reference()
    {
        var date = new DateOnly(2026, 10, 3);
        var receipt = new AdmissionMaterialization.Receipt(Guid.NewGuid(), Guid.NewGuid(), date, "ACTA SYNTHETIC");
        Assert.True(AdmissionMaterialization.Matches(receipt, new(date, " ACTA SYNTHETIC ")));
        Assert.False(AdmissionMaterialization.Matches(receipt, new(date.AddDays(-1), "ACTA SYNTHETIC")));
        Assert.False(AdmissionMaterialization.Matches(receipt, new(date, "OTHER ACTA")));
    }
}
