namespace PMGM.Api.Modules.Ceremonies;

public sealed record CeremonyEligibilityInput(
    string CeremonyType,
    IReadOnlyDictionary<string, string> Validations,
    DateTimeOffset? PublicationStartedAtUtc,
    DateTimeOffset? PublicationEndedAtUtc,
    int PublicationRequiredDays,
    bool PublicationSuspended = false);

public sealed record CeremonyEligibilityResult(
    bool IsEligible,
    string Status,
    IReadOnlyList<string> BlockingReasons,
    int? PublicationElapsedDays = null,
    int? PublicationRequiredDays = null);

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
        var blockingReasons = new List<string>();

        RequireValidation(input, CeremonyCodes.ValidationType.InternalAffairs, blockingReasons);
        RequireValidation(input, CeremonyCodes.ValidationType.Treasury, blockingReasons);
        RequireValidation(input, CeremonyCodes.ValidationType.Hospitalaria, blockingReasons);

        int? publicationElapsedDays = null;

        if (string.Equals(input.CeremonyType, CeremonyCodes.Type.Initiation, StringComparison.OrdinalIgnoreCase))
        {
            if (input.PublicationRequiredDays < 1)
            {
                blockingReasons.Add("La regla de días mínimos de publicación no es válida.");
            }
            else if (input.PublicationStartedAtUtc is null)
            {
                blockingReasons.Add("El insinuado aún no posee una publicación válida.");
            }
            else if (input.PublicationSuspended)
            {
                blockingReasons.Add("La publicación del insinuado se encuentra suspendida.");
            }
            else
            {
                var effectiveEnd = input.PublicationEndedAtUtc ?? nowUtc;
                publicationElapsedDays = Math.Max(
                    0,
                    (int)Math.Floor((effectiveEnd - input.PublicationStartedAtUtc.Value).TotalDays));

                if (publicationElapsedDays < input.PublicationRequiredDays)
                {
                    blockingReasons.Add(
                        $"La publicación del insinuado registra {publicationElapsedDays} días y requiere {input.PublicationRequiredDays} días.");
                }
            }

            RequireValidation(input, CeremonyCodes.ValidationType.CandidatePublication, blockingReasons);
        }

        return new CeremonyEligibilityResult(
            IsEligible: blockingReasons.Count == 0,
            Status: blockingReasons.Count == 0 ? CeremonyCodes.RequestStatus.Eligible : CeremonyCodes.RequestStatus.Observed,
            BlockingReasons: blockingReasons,
            PublicationElapsedDays: publicationElapsedDays,
            PublicationRequiredDays: string.Equals(input.CeremonyType, CeremonyCodes.Type.Initiation, StringComparison.OrdinalIgnoreCase)
                ? input.PublicationRequiredDays
                : null);
    }

    private static void RequireValidation(
        CeremonyEligibilityInput input,
        string validationType,
        ICollection<string> blockingReasons)
    {
        if (!input.Validations.TryGetValue(validationType, out var status))
        {
            blockingReasons.Add($"Falta validación obligatoria: {validationType}.");
            return;
        }

        if (!EnablingStatuses.Contains(status))
        {
            blockingReasons.Add($"La validación {validationType} no se encuentra aprobada: {status}.");
        }
    }
}
