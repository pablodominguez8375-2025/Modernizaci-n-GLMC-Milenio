namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// El intento fallido de autorización no es una sentencia ni un rechazo formal:
/// se subsanan observaciones en el mismo expediente. Los estados terminales no
/// pueden regresar a autorizado por el mero cambio de una validación externa.
/// </summary>
public static class CeremonyAuthorizationStatePolicy
{
    public const string RequirementCode = "ceremony_request_status";

    public static bool CanAttemptAuthorization(string? requestStatus)
        => requestStatus is CeremonyCodes.RequestStatus.Draft
            or CeremonyCodes.RequestStatus.UnderReview
            or CeremonyCodes.RequestStatus.Eligible
            or CeremonyCodes.RequestStatus.Observed;

    public static string AfterUnmetRequirements(string currentStatus)
        => CanAttemptAuthorization(currentStatus)
            ? CeremonyCodes.RequestStatus.Observed
            : currentStatus;

    public static CeremonyRequirementResult? EvaluateRequirement(string? requestStatus)
    {
        if (requestStatus is null || CanAttemptAuthorization(requestStatus))
            return null;

        var reason = requestStatus switch
        {
            CeremonyCodes.RequestStatus.Authorized => "La solicitud ya fue autorizada; no admite una segunda autorización.",
            CeremonyCodes.RequestStatus.Completed => "La ceremonia fue registrada como realizada y no puede reautorizarse.",
            CeremonyCodes.RequestStatus.Rejected => "El expediente fue formalmente rechazado y no puede reabrirse mediante autorización.",
            _ => "El expediente tiene un estado no habilitado para autorización."
        };
        return new(RequirementCode, "Estado del expediente",
            CeremonyCodes.ValidationStatus.Rejected, reason);
    }
}
