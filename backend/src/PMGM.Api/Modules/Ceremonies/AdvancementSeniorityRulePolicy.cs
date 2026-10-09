using System.Text.Json;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Versiones independientes del mínimo de antigüedad de art. 3.2 d/e.
/// Sin norma referenciada y vigente no se presume ningún mínimo.
/// Esta política sólo revisa evidencia; NO certifica continuidad ni autoriza.
/// </summary>
public static class AdvancementSeniorityRulePolicy
{
    public const string ApprenticeCode = "ceremony.advancement.apprentice.seniority_months";
    public const string FellowcraftCode = "ceremony.advancement.fellowcraft.seniority_months";

    public static string? CodeFor(string ceremonyType) => ceremonyType switch
    {
        CeremonyCodes.Type.WageIncrease => ApprenticeCode,
        CeremonyCodes.Type.Exaltation => FellowcraftCode,
        _ => null
    };

    public static bool IsValidMinimum(int minimumCompleteMonths) => minimumCompleteMonths > 0;

    public static string Serialize(int minimumCompleteMonths)
    {
        if (!IsValidMinimum(minimumCompleteMonths))
            throw new ArgumentOutOfRangeException(nameof(minimumCompleteMonths));
        return JsonSerializer.Serialize(new { minimumCompleteMonths });
    }

    public static bool TryParse(string? value, out int minimumCompleteMonths)
    {
        minimumCompleteMonths = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            using var document = JsonDocument.Parse(value);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("minimumCompleteMonths", out var element) ||
                element.ValueKind != JsonValueKind.Number ||
                !element.TryGetInt32(out var number) ||
                !IsValidMinimum(number))
                return false;
            minimumCompleteMonths = number;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static AdvancementSeniorityRuleSnapshot? Resolve(
        string ceremonyType, DateOnly asOf,
        IEnumerable<InstitutionalRuleSetting> institutionalRules)
    {
        var code = CodeFor(ceremonyType);
        if (code is null) return null;
        var matching = institutionalRules.Where(x =>
            x.Code == code &&
            x.Status == "active" &&
            x.EffectiveFrom <= asOf &&
            (x.EffectiveTo is null || x.EffectiveTo >= asOf)).ToArray();
        // Sin regla o con solapamiento de vigencias se rechaza: fail closed.
        if (matching.Length != 1) return null;
        var rule = matching[0];
        if (rule.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(rule.SourceReference) ||
            !TryParse(rule.Value, out var minimum))
            return null;
        return new AdvancementSeniorityRuleSnapshot(rule.Id, rule.Code,
            rule.EffectiveFrom, rule.EffectiveTo, rule.SourceReference, minimum);
    }

    public static AdvancementSeniorityRuleReview Review(
        AdvancementSeniorityRuleSnapshot? rule,
        AdvancementSenioritySnapshot? evidence)
    {
        if (rule is null)
            return new("institutional_rule_missing", 0, evidence?.CompleteCalendarMonths,
                false, false, false, null,
                "No existe regla institucional de antigüedad vigente, unívoca y respaldada.");
        if (evidence is null)
            return new("seniority_evidence_missing", rule.MinimumCompleteMonths, null,
                false, false, false, rule.RuleId.ToString("N"),
                "Falta la evidencia cronológica del grado y las membresías.");
        if (!evidence.MembershipDateCoverageComplete ||
            evidence.HasInstitutionalInterruption ||
            evidence.ContinuityEvidenceStatus != "membership_dates_covered")
            return new("continuity_unverified", rule.MinimumCompleteMonths,
                evidence.CompleteCalendarMonths, false, false, false, rule.RuleId.ToString("N"),
                "Existe un vacío, interrupción o inconsistencia de membresía; verificar continuidad institucional.");
        if (evidence.CompleteCalendarMonths < rule.MinimumCompleteMonths)
            return new("minimum_not_reached", rule.MinimumCompleteMonths,
                evidence.CompleteCalendarMonths, false, false, false, rule.RuleId.ToString("N"),
                "Los meses completos registrados no alcanzan el mínimo de la regla vigente.");

        // Cubrir las fechas no prueba por sí solo el estatus institucional:
        // toda valoración definitiva se mantiene pendiente.
        return new("threshold_reached_pending_certification", rule.MinimumCompleteMonths,
            evidence.CompleteCalendarMonths, true, false, false, rule.RuleId.ToString("N"),
            "El mínimo cronológico se alcanzó, pero la continuidad requiere certificación institucional.");
    }
}

public sealed record AdvancementSeniorityRuleSnapshot(
    Guid RuleId,
    string RuleCode,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string SourceReference,
    int MinimumCompleteMonths);

public sealed record AdvancementSeniorityRuleReview(
    string Status,
    int MinimumCompleteMonths,
    int? CompleteCalendarMonths,
    bool ChronologicalThresholdReached,
    bool InstitutionalContinuityCertified,
    bool AuthorizesCeremony,
    string? RuleVersion,
    string Reason);
