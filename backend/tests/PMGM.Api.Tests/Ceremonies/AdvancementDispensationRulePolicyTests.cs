using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using Xunit;

namespace PMGM.Api.Tests.Ceremonies;

public sealed class AdvancementDispensationRulePolicyTests
{
    [Fact]
    public void Resolve_RequiresOneReferencedUnanimousMasterRule()
    {
        var day = new DateOnly(2026, 10, 10);
        var rule = new InstitutionalRuleSetting {
            Code = AdvancementDispensationRulePolicy.Code,
            Value = """{"decision":"unanimous","meetingGrade":"master","maximumReductionPercent":50}""",
            EffectiveFrom = day, Status = "active", SourceReference = "Reglamento General art. 3.4"
        };
        var result = AdvancementDispensationRulePolicy.Resolve(day, new[] { rule });
        var resolved = Assert.IsType<AdvancementDispensationRuleSnapshot>(result);
        Assert.Equal(50, resolved.MaximumReductionPercent);
    }

    [Theory]
    [InlineData("""{"decision":"two_thirds","meetingGrade":"master","maximumReductionPercent":50}""")]
    [InlineData("""{"decision":"unanimous","meetingGrade":"master","maximumReductionPercent":51}""")]
    [InlineData("""{"decision":"unanimous","meetingGrade":"fellowcraft","maximumReductionPercent":50}""")]
    public void Resolve_RejectsRulesThatDiluteArticle34(string value)
    {
        var day = new DateOnly(2026, 10, 10);
        var rule = new InstitutionalRuleSetting {
            Code = AdvancementDispensationRulePolicy.Code, Value = value,
            EffectiveFrom = day, Status = "active", SourceReference = "RG 3.4"
        };
        Assert.Null(AdvancementDispensationRulePolicy.Resolve(day, new[] { rule }));
    }
}
