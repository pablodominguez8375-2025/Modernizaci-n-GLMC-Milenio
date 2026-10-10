using System.Text.Json;
using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementAuthorizationSnapshotTests
{
    [Fact]
    public void EvidenceIds_AreAuditable_ButNeverSerializedInEligibilityResponse()
    {
        var meeting = Guid.NewGuid();
        var instruction = Guid.NewGuid();
        var document = Guid.NewGuid();
        var thresholds = new AdvancementThresholds(6, 3, 2, Guid.NewGuid().ToString("N"));
        var decision = new AdvancementEligibilityDecision(
            true, true, AdvancementEligibilityModes.Ordinary, 1, thresholds.RuleVersion,
            [
                new AdvancementRequirementResult(AdvancementRequirementCodes.MeetingAttendance,
                    "Tenidas", 6, 7, true, CeremonyCodes.ValidationStatus.Approved),
                new AdvancementRequirementResult(AdvancementRequirementCodes.InstructionAttendance,
                    "Docencia", 3, 4, true, CeremonyCodes.ValidationStatus.Approved),
                new AdvancementRequirementResult(AdvancementRequirementCodes.WorkPapers,
                    "Planchas", 2, 2, true, CeremonyCodes.ValidationStatus.Approved)
            ], null);
        var snapshot = new AdvancementAuthorizationSnapshot(
            Guid.NewGuid(), thresholds.RuleVersion, Guid.NewGuid(),
            new DateOnly(2025, 10, 1), new DateOnly(2026, 10, 10),
            7, 4, 2, true, true, thresholds,
            [document], [meeting], [instruction], decision);

        Assert.Equal(meeting, Assert.Single(snapshot.VerifiedMeetingAttendanceIds));
        Assert.Equal(instruction, Assert.Single(snapshot.VerifiedInstructionAttendanceIds));
        Assert.Equal(document, Assert.Single(snapshot.CertifiedPaperDocumentIds));

        var body = JsonSerializer.Serialize(snapshot);
        Assert.Contains("MinimumMeetingAttendance", body, StringComparison.Ordinal);
        Assert.Contains("MeetingAttendance", body, StringComparison.Ordinal);
        Assert.DoesNotContain(document.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(meeting.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(instruction.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EvidenceIds", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CertifiedPaperDocumentIds", body, StringComparison.Ordinal);
    }
}
