using PMGM.Api.Modules.Ceremonies;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementDispensationReductionPolicyTests
{
    [Theory]
    [InlineData(AdvancementDispensationReductionPolicy.SeniorityMonths, 24, 12, 12)]
    [InlineData(AdvancementRequirementCodes.MeetingAttendance, 30, 15, 15)]
    [InlineData(AdvancementRequirementCodes.InstructionAttendance, 10, 5, 5)]
    [InlineData(AdvancementRequirementCodes.MeetingAttendance, 5, 2, 3)]
    public void AuthorizedReductions_RespectHalfOfOriginalMinimum(
        string code, int minimum, int reduction, int expected)
    {
        Assert.True(AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum(
            code, minimum, reduction, out var effective));
        Assert.Equal(expected, effective);
    }

    [Theory]
    [InlineData(AdvancementDispensationReductionPolicy.SeniorityMonths, 24, 13)]
    [InlineData(AdvancementRequirementCodes.MeetingAttendance, 30, 16)]
    [InlineData(AdvancementRequirementCodes.InstructionAttendance, 5, 3)]
    [InlineData(AdvancementRequirementCodes.WorkPapers, 2, 1)]
    [InlineData("unknown_requirement", 10, 1)]
    [InlineData(AdvancementRequirementCodes.MeetingAttendance, -1, 0)]
    [InlineData(AdvancementRequirementCodes.MeetingAttendance, 10, -1)]
    public void ExcessiveOrForbiddenReduction_IsRejected(
        string code, int minimum, int reduction)
    {
        Assert.False(AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum(
            code, minimum, reduction, out _));
    }

    [Fact]
    public void WorkPapers_CannotBeReducedEvenByCouncil()
    {
        Assert.True(AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum(
            AdvancementRequirementCodes.WorkPapers, 2, 0, out var unchanged));
        Assert.Equal(2, unchanged);

        Assert.False(AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum(
            AdvancementRequirementCodes.WorkPapers, 2, 1, out _));
    }

    [Fact]
    public void ZeroReduction_NeverRelaxesTheRule()
    {
        Assert.True(AdvancementDispensationReductionPolicy.TryCalculateEffectiveMinimum(
            AdvancementDispensationReductionPolicy.SeniorityMonths, 24, 0, out var effective));
        Assert.Equal(24, effective);
    }
}
