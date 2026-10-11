using System.Text.Json;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

public static class AdvancementDispensationRulePolicy
{
    public const string Code = "ceremony.advancement.dispensation";

    public static AdvancementDispensationRuleSnapshot? Resolve(
        DateOnly asOf, IEnumerable<InstitutionalRuleSetting> rules)
    {
        var matching = rules.Where(x => x.Code == Code && x.Status == "active" &&
            x.EffectiveFrom <= asOf && (x.EffectiveTo == null || x.EffectiveTo >= asOf)).ToArray();
        if (matching.Length != 1) return null;
        var rule = matching[0];
        if (rule.Id == Guid.Empty || string.IsNullOrWhiteSpace(rule.SourceReference) ||
            !TryParse(rule.Value, out var maximum) || maximum != 50) return null;
        return new(rule.Id, rule.SourceReference, maximum);
    }

    public static bool TryParse(string? value, out int maximumReductionPercent)
    {
        maximumReductionPercent = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            using var doc = JsonDocument.Parse(value);
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("decision", out var decision) &&
                decision.GetString() == "unanimous" &&
                root.TryGetProperty("meetingGrade", out var grade) &&
                grade.GetString() == "master" &&
                root.TryGetProperty("maximumReductionPercent", out var maximum) &&
                maximum.TryGetInt32(out maximumReductionPercent) &&
                maximumReductionPercent == 50;
        }
        catch (JsonException) { return false; }
    }
}

public sealed record AdvancementDispensationRuleSnapshot(
    Guid RuleId, string SourceReference, int MaximumReductionPercent);

public sealed record AdvancementDispensationReduction(string RequirementCode, int AuthorizedReduction);

public static class AdvancementDispensationCaseStatuses
{
    public const string CouncilApproved = "council_approved";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}
