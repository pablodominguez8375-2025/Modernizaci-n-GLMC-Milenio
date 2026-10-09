using System.Text.Json;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Versiona en una sola regla los tres mínimos del grado saliente. No hay
/// valor por defecto: sin resolución institucional válida no hay elegibilidad.
/// </summary>
public static class AdvancementRulePolicy
{
    public const string ApprenticeCode = "ceremony.advancement.apprentice.thresholds";
    public const string FellowcraftCode = "ceremony.advancement.fellowcraft.thresholds";

    public static string? CodeFor(string ceremonyType)
        => ceremonyType switch
        {
            CeremonyCodes.Type.WageIncrease => ApprenticeCode,
            CeremonyCodes.Type.Exaltation => FellowcraftCode,
            _ => null
        };

    public static bool IsValid(AdvancementThresholdConfiguration value)
        => value.MinimumMeetingAttendance >= 0 &&
           value.MinimumInstructionAttendance >= 0 &&
           value.MinimumWorkPapers >= 0;

    public static string Serialize(AdvancementThresholdConfiguration value)
        => JsonSerializer.Serialize(new
        {
            minimumMeetingAttendance = value.MinimumMeetingAttendance,
            minimumInstructionAttendance = value.MinimumInstructionAttendance,
            minimumWorkPapers = value.MinimumWorkPapers
        });

    public static AdvancementRuleSnapshot? Resolve(
        string ceremonyType,
        DateOnly asOf,
        IEnumerable<InstitutionalRuleSetting> institutionalRules)
    {
        var code = CodeFor(ceremonyType);
        if (code is null) return null;

        var matching = institutionalRules
            .Where(rule => rule.Code == code &&
                           rule.Status == "active" &&
                           rule.EffectiveFrom <= asOf &&
                           (rule.EffectiveTo is null || rule.EffectiveTo >= asOf))
            .ToArray();

        // Períodos solapados no son una selección válida y no pueden depender
        // de un ordenamiento accidental de EF o de la fecha de creación.
        if (matching.Length != 1) return null;

        var selected = matching[0];
        if (string.IsNullOrWhiteSpace(selected.SourceReference) ||
            !TryParse(selected.Value, out var thresholds))
            return null;

        return new AdvancementRuleSnapshot(
            selected.Id,
            selected.Code,
            selected.EffectiveFrom,
            selected.EffectiveTo,
            selected.SourceReference,
            thresholds,
            new AdvancementThresholds(
                thresholds.MinimumMeetingAttendance,
                thresholds.MinimumInstructionAttendance,
                thresholds.MinimumWorkPapers,
                selected.Id.ToString("N")));
    }

    public static bool TryParse(string json, out AdvancementThresholdConfiguration value)
    {
        value = new(0, 0, 0);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !ReadInteger(doc.RootElement, "minimumMeetingAttendance", out var meetings) ||
                !ReadInteger(doc.RootElement, "minimumInstructionAttendance", out var instructions) ||
                !ReadInteger(doc.RootElement, "minimumWorkPapers", out var papers))
                return false;

            var parsed = new AdvancementThresholdConfiguration(meetings, instructions, papers);
            if (!IsValid(parsed)) return false;
            value = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ReadInteger(JsonElement root, string property, out int value)
    {
        value = 0;
        return root.TryGetProperty(property, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt32(out value);
    }
}

public sealed record AdvancementThresholdConfiguration(
    int MinimumMeetingAttendance,
    int MinimumInstructionAttendance,
    int MinimumWorkPapers);

public sealed record AdvancementRuleSnapshot(
    Guid RuleId,
    string RuleCode,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourceReference,
    AdvancementThresholdConfiguration Configuration,
    AdvancementThresholds Thresholds);
