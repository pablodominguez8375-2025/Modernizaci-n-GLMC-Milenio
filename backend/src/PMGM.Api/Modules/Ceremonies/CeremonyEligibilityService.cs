using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.Ceremonies;

public sealed record CeremonyEligibilityInput(
    string CeremonyType,
    IReadOnlyDictionary<string, string> Validations,
    DateTimeOffset? PublicationStartedAtUtc,
    DateTimeOffset? PublicationEndedAtUtc,
    int PublicationRequiredDays,
    bool PublicationSuspended = false,
    AdvancementThresholds? AdvancementThresholds = null,
    AdvancementEvidence? AdvancementEvidence = null,
    DispensationEvidence? Dispensation = null);

public sealed record CeremonyEligibilityResult(
    bool IsEligible,
    string Status,
    IReadOnlyList<string> BlockingReasons,
    int? PublicationElapsedDays = null,
    int? PublicationRequiredDays = null,
    AdvancementEligibilityDecision? Advancement = null);

public interface ICeremonyEligibilityService
{
    CeremonyEligibilityResult Evaluate(CeremonyEligibilityInput input, DateTimeOffset nowUtc);
}

public sealed class CeremonyEligibilityService : ICeremonyEligibilityService
{
    private static readonly HashSet<string> EnablingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        CeremonyCodes.ValidationStatus.Approved,
        CeremonyCodes.ValidationStatus.NotApplicable,
        CeremonyCodes.ValidationStatus.ExceptionApproved
    };

    public CeremonyEligibilityResult Evaluate(CeremonyEligibilityInput input, DateTimeOffset nowUtc)
    {
        var regimenStatus = GetValidation(input, CeremonyCodes.ValidationType.InternalAffairs);
        var treasuryValidation = GetValidation(input, CeremonyCodes.ValidationType.Treasury);
        var hospitalariaValidation = GetValidation(input, CeremonyCodes.ValidationType.Hospitalaria);

        var treasuryStatus = treasuryValidation is null
            ? null
            : EnablingStatuses.Contains(treasuryValidation)
                ? TreasuryCodes.RegularityStatus.UpToDate
                : TreasuryCodes.RegularityStatus.Delinquent;

        var hospitalariaStatus = hospitalariaValidation is null
            ? null
            : EnablingStatuses.Contains(hospitalariaValidation)
                ? HospitalariaCodes.RegularityStatus.UpToDate
                : HospitalariaCodes.RegularityStatus.Overdue;

        CandidatePublicationEvidence? publication = null;
        int? elapsedDays = null;

        if (string.Equals(input.CeremonyType, CeremonyCodes.Type.Initiation, StringComparison.OrdinalIgnoreCase) &&
            input.PublicationStartedAtUtc is not null)
        {
            var effectiveEnd = input.PublicationEndedAtUtc ?? nowUtc;
            elapsedDays = Math.Max(
                0,
                (int)Math.Floor((effectiveEnd - input.PublicationStartedAtUtc.Value).TotalDays));

            var publicationValidation = GetValidation(input, CeremonyCodes.ValidationType.CandidatePublication);
            var publicationStatus = input.PublicationSuspended
                ? CeremonyCodes.PublicationStatus.Suspended
                : publicationValidation is null || !EnablingStatuses.Contains(publicationValidation)
                    ? CeremonyCodes.PublicationStatus.Cancelled
                    : input.PublicationEndedAtUtc is null
                        ? CeremonyCodes.PublicationStatus.Published
                        : CeremonyCodes.PublicationStatus.Completed;

            publication = new CandidatePublicationEvidence(
                Guid.Empty,
                publicationStatus,
                input.PublicationRequiredDays,
                elapsedDays.Value,
                CeremonyCodes.Rules.InitiationPublicationMinimumDays);
        }

        var decision = CeremonyEligibilityPolicy.Evaluate(
            input.CeremonyType,
            regimenStatus,
            treasuryStatus,
            hospitalariaStatus,
            publication);

        var blockingReasons = decision.Requirements
            .Where(x => x.Status != CeremonyCodes.ValidationStatus.Approved)
            .Select(x => $"{MapRequirementCodeToValidationType(x.Code)}: {x.Reason}")
            .ToList();

        AdvancementEligibilityDecision? advancement = null;
        if (input.AdvancementThresholds is not null && input.AdvancementEvidence is not null)
        {
            advancement = AdvancementEligibilityPolicy.Evaluate(
                input.CeremonyType,
                input.AdvancementThresholds,
                input.AdvancementEvidence,
                input.Dispensation);

            if (advancement.Applies && !advancement.CanProceed)
            {
                blockingReasons.AddRange(
                    advancement.Requirements
                        .Where(x => !x.Complies)
                        .Select(x => $"advancement.{x.Code}: alcanzado {x.Achieved}; mínimo {x.Minimum}."));

                if (advancement.Dispensation is not null)
                {
                    blockingReasons.Add($"dispensation: {advancement.Dispensation.Reason}");
                }
            }
        }

        var isEligible = decision.CanAuthorize && (advancement?.CanProceed ?? true);

        return new CeremonyEligibilityResult(
            IsEligible: isEligible,
            Status: isEligible ? CeremonyCodes.RequestStatus.Eligible : CeremonyCodes.RequestStatus.Observed,
            BlockingReasons: blockingReasons,
            PublicationElapsedDays: string.Equals(input.CeremonyType, CeremonyCodes.Type.Initiation, StringComparison.OrdinalIgnoreCase)
                ? elapsedDays
                : null,
            PublicationRequiredDays: string.Equals(input.CeremonyType, CeremonyCodes.Type.Initiation, StringComparison.OrdinalIgnoreCase)
                ? input.PublicationRequiredDays
                : null,
            Advancement: advancement);
    }

    private static string? GetValidation(CeremonyEligibilityInput input, string validationType)
        => input.Validations.TryGetValue(validationType, out var status) ? status : null;

    private static string MapRequirementCodeToValidationType(string code)
        => code switch
        {
            "regimen_interior" => CeremonyCodes.ValidationType.InternalAffairs,
            "gran_tesoreria" => CeremonyCodes.ValidationType.Treasury,
            "gran_hospitalaria" => CeremonyCodes.ValidationType.Hospitalaria,
            "publicacion_insinuado" => CeremonyCodes.ValidationType.CandidatePublication,
            _ => code
        };
}
