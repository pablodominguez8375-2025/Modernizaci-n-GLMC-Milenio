using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.LodgeManagement;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementReviewMatrixPolicyTests
{
    private static readonly DateOnly Start = new(2024, 9, 9);
    private static readonly DateOnly Cutoff = new(2026, 10, 9);

    [Fact]
    public void AllObservedThresholdsReached_StillCannotAuthorizeWithoutInstitutionalAccreditation()
    {
        var matrix = AdvancementReviewMatrixPolicy.Build(
            CeremonyCodes.Type.WageIncrease,
            Attendance(5, 3, 2, 1),
            CountRule(4, 2, 2),
            new AdvancementSeniorityRuleReview(
                "threshold_reached_pending_certification", 24, 25,
                true, false, false, "seniority-v1", "Falta certificación."));

        Assert.Equal(4, matrix.Requirements.Count);
        Assert.False(matrix.AuthorizesCeremony);
        Assert.False(matrix.AllRequirementsCertified);
        Assert.Equal("pending_institutional_validation", matrix.Status);
        Assert.Contains(matrix.Requirements, x => x.Code == "meeting_attendance" &&
            x.Observed == 5 && x.Required == 4 &&
            x.Status == "threshold_reached_pending_verification" && !x.InstitutionallyCertified);
        Assert.Contains(matrix.Requirements, x => x.Code == "instruction_attendance" &&
            x.Observed == 3 && x.Status == "threshold_reached_pending_verification");
        Assert.Contains(matrix.Requirements, x => x.Code == "work_papers" &&
            x.Observed is null && x.Status == "presentation_unverified");
        Assert.Contains(matrix.Requirements, x => x.Code == "complete_months" &&
            x.Observed == 25 && !x.InstitutionallyCertified);
    }

    [Fact]
    public void MissingInstitutionalRules_NeverDefaultToHistoricalDemoThresholds()
    {
        var matrix = AdvancementReviewMatrixPolicy.Build(
            CeremonyCodes.Type.Exaltation, Attendance(30, 20, 0, 0, "fellowcraft"),
            null,
            AdvancementSeniorityRulePolicy.Review(null, null));

        Assert.All(matrix.Requirements, x => Assert.False(x.InstitutionallyCertified));
        Assert.Equal(4, matrix.Requirements.Count(x => x.Status == "rule_missing" ||
                        x.Status == "institutional_rule_missing"));
        Assert.Equal("rule_missing", matrix.Requirements[0].Status);
        Assert.Null(matrix.Requirements[0].Required);
        Assert.Null(matrix.Requirements[2].Observed);
        Assert.False(matrix.AuthorizesCeremony);
    }

    [Fact]
    public void ExcusesDoNotCountAsPresencesAndDoNotReachMinimum()
    {
        var matrix = AdvancementReviewMatrixPolicy.Build(
            CeremonyCodes.Type.WageIncrease, Attendance(3, 1, 8, 2),
            CountRule(4, 2, 2),
            AdvancementSeniorityRulePolicy.Review(null, null));

        Assert.Contains(matrix.Requirements, x => x.Code == "meeting_attendance" &&
            x.Observed == 3 && x.Status == "minimum_not_reached");
        Assert.Contains(matrix.Requirements, x => x.Code == "instruction_attendance" &&
            x.Observed == 1 && x.Status == "minimum_not_reached");
        Assert.False(matrix.AuthorizesCeremony);
    }

    [Fact]
    public void MissingDegreeEvidence_AndMismatchedGrade_DoNotCreditCounts()
    {
        var wrongGrade = AdvancementReviewMatrixPolicy.Build(
            CeremonyCodes.Type.WageIncrease,
            Attendance(50, 20, 0, 0, "fellowcraft"),
            CountRule(4, 2, 2),
            AdvancementSeniorityRulePolicy.Review(null, null));
        Assert.All(wrongGrade.Requirements.Take(2), x =>
            Assert.Equal("evidence_missing", x.Status));

        var missing = AdvancementReviewMatrixPolicy.Build(
            CeremonyCodes.Type.WageIncrease,
            new AdvancementAttendanceResult("missing_degree_start", "Sin iniciación.", null),
            CountRule(4, 2, 2),
            AdvancementSeniorityRulePolicy.Review(null, null));
        Assert.All(missing.Requirements.Take(2), x =>
            Assert.Null(x.Observed));
        Assert.Null(missing.GradeStartDate);
        Assert.False(missing.AuthorizesCeremony);
    }

    private static AdvancementAttendanceResult Attendance(
        int meetingsPresent, int instructionsPresent,
        int excusedMeetings, int excusedInstructions,
        string sourceGrade = LodgeManagementCodes.Grade.Apprentice)
        => new("ready", null, new AdvancementAttendanceSnapshot(
            Guid.NewGuid(), Guid.NewGuid(),
            sourceGrade == LodgeManagementCodes.Grade.Fellowcraft
                ? CeremonyCodes.Type.Exaltation : CeremonyCodes.Type.WageIncrease,
            sourceGrade, Start, Cutoff,
            new AttendanceCounts(meetingsPresent + excusedMeetings,
                meetingsPresent + excusedMeetings, meetingsPresent,
                excusedMeetings, 0, []),
            new AttendanceCounts(instructionsPresent + excusedInstructions,
                instructionsPresent + excusedInstructions, instructionsPresent,
                excusedInstructions, 0, [])));

    private static AdvancementRuleSnapshot CountRule(int meetings, int instructions, int papers)
        => new(Guid.NewGuid(), AdvancementRulePolicy.ApprenticeCode, Cutoff, null, "ACTA-CI",
            new(meetings, instructions, papers),
            new(meetings, instructions, papers, "rules-v1"));
}
