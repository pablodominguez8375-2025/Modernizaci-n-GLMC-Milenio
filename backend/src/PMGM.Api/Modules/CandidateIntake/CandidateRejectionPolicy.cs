using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>
/// Reglas de bloqueo transversal para nuevas presentaciones después de un rechazo.
/// Conforme al protocolo 2026, un rechazo en la votación abierta de tercer grado
/// o en el balotaje definitivo de primer grado activa el antecedente de la Orden.
/// </summary>
public static class CandidateRejectionPolicy
{
    public static bool IsOrderBlocking(string validationType, string status)
        => status == CeremonyCodes.ValidationStatus.Rejected &&
           validationType is CeremonyCodes.ValidationType.CandidateThirdDegreeReview
               or CeremonyCodes.ValidationType.CandidateFinalBallot;

    public static string GetReason(string validationType)
        => validationType == CeremonyCodes.ValidationType.CandidateFinalBallot
            ? "Rechazo en balotaje de 1.er grado"
            : "Rechazo en Cámara del Medio / revisión de 3.er grado";
}
