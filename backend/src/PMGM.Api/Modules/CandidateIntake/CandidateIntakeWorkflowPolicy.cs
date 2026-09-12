namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>
/// Reglas de dominio del procedimiento de insinuación 2026.
/// Los plazos se validan en backend y los valores configurables se reciben como parámetros.
/// </summary>
public static class CandidateIntakeWorkflowPolicy
{
    public static CandidateWorkflowDecision EvaluateInitialDeliberation(
        DateOnly presentationDate,
        DateOnly deliberationDate,
        int presentVoters,
        int votesInFavor,
        int minimumWaitingDays = 7)
    {
        if (minimumWaitingDays < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumWaitingDays));
        if (deliberationDate < presentationDate)
            return CandidateWorkflowDecision.Blocked("initial_deliberation.date", "La deliberación no puede ser anterior a la presentación de la insinuación.");
        if (presentVoters <= 0)
            return CandidateWorkflowDecision.Blocked("initial_deliberation.quorum", "Debe existir al menos una persona habilitada presente para registrar la votación.");
        if (votesInFavor < 0 || votesInFavor > presentVoters)
            return CandidateWorkflowDecision.Blocked("initial_deliberation.votes", "La cantidad de votos favorables no es válida.");

        var elapsedDays = deliberationDate.DayNumber - presentationDate.DayNumber;
        if (elapsedDays < minimumWaitingDays)
        {
            return CandidateWorkflowDecision.Blocked(
                "initial_deliberation.waiting_period",
                $"Deben transcurrir al menos {minimumWaitingDays} días desde la presentación; han transcurrido {elapsedDays}.");
        }

        if (votesInFavor != presentVoters)
        {
            return CandidateWorkflowDecision.Rejected(
                "initial_deliberation.unanimity",
                "La aprobación inicial requiere unanimidad de las personas presentes.");
        }

        return CandidateWorkflowDecision.Allowed(
            "initial_deliberation.approved",
            "Se cumple el plazo mínimo y la votación inicial fue unánime.");
    }

    public static CandidateWorkflowDecision EvaluateInterviewPackage(
        int completedInterviews,
        bool confidentialQuestionnaireAvailable,
        bool autobiographyAvailable)
    {
        if (completedInterviews < 0)
            throw new ArgumentOutOfRangeException(nameof(completedInterviews));

        if (completedInterviews != 3)
        {
            return CandidateWorkflowDecision.Blocked(
                "third_degree_review.interviews",
                $"El expediente requiere tres entrevistas completas; actualmente registra {completedInterviews}.");
        }

        if (!confidentialQuestionnaireAvailable)
        {
            return CandidateWorkflowDecision.Blocked(
                "third_degree_review.confidential_questionnaire",
                "Falta el Cuestionario Confidencial requerido para la revisión de tercer grado.");
        }

        if (!autobiographyAvailable)
        {
            return CandidateWorkflowDecision.Blocked(
                "third_degree_review.autobiography",
                "Falta la autobiografía requerida para la revisión de tercer grado.");
        }

        return CandidateWorkflowDecision.Allowed(
            "third_degree_review.package_complete",
            "El expediente contiene las tres entrevistas, el Cuestionario Confidencial y la autobiografía.");
    }

    public static CandidateWorkflowDecision EvaluateThirdDegreeOpenVote(bool approved)
        => approved
            ? CandidateWorkflowDecision.Allowed(
                "third_degree_review.approved",
                "La votación abierta de tercer grado fue favorable.")
            : CandidateWorkflowDecision.Rejected(
                "third_degree_review.rejected",
                "La votación abierta de tercer grado no fue favorable.");

    public static CandidateWorkflowDecision EvaluateFinalBallot(
        DateOnly publicationDate,
        DateOnly ballotDate,
        bool thirdDegreeOpenVoteApproved,
        int minimumPublicationDays)
    {
        if (minimumPublicationDays < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumPublicationDays));
        if (!thirdDegreeOpenVoteApproved)
        {
            return CandidateWorkflowDecision.Blocked(
                "first_degree_ballot.third_degree_review",
                "No puede realizarse el balotaje mientras la votación abierta de tercer grado no esté aprobada.");
        }
        if (ballotDate < publicationDate)
        {
            return CandidateWorkflowDecision.Blocked(
                "first_degree_ballot.date",
                "La fecha del balotaje no puede ser anterior a la publicación.");
        }

        var elapsedDays = ballotDate.DayNumber - publicationDate.DayNumber;
        if (elapsedDays < minimumPublicationDays)
        {
            return CandidateWorkflowDecision.Blocked(
                "first_degree_ballot.publication_period",
                $"Deben transcurrir al menos {minimumPublicationDays} días corridos desde la publicación; han transcurrido {elapsedDays}.");
        }

        return CandidateWorkflowDecision.Allowed(
            "first_degree_ballot.ready",
            "El expediente cumple la revisión de tercer grado y el plazo mínimo de publicación para efectuar el balotaje.");
    }

    public static CandidateWorkflowDecision EvaluateRePresentation(
        DateOnly rejectionDate,
        DateOnly newPresentationDate,
        bool rejectionCausesRemedied)
    {
        if (newPresentationDate < rejectionDate)
        {
            return CandidateWorkflowDecision.Blocked(
                "representation.date",
                "La nueva presentación no puede ser anterior al rechazo previo.");
        }

        var earliestDate = rejectionDate.AddYears(1);
        if (newPresentationDate < earliestDate)
        {
            return CandidateWorkflowDecision.Blocked(
                "representation.minimum_year",
                $"La nueva presentación sólo puede efectuarse desde el {earliestDate:yyyy-MM-dd}.");
        }

        if (!rejectionCausesRemedied)
        {
            return CandidateWorkflowDecision.Blocked(
                "representation.causes_remedied",
                "Debe quedar constancia de que fueron subsanadas las causas del rechazo anterior.");
        }

        return CandidateWorkflowDecision.Allowed(
            "representation.allowed",
            "Ha transcurrido al menos un año y consta la subsanación de las causas del rechazo anterior.");
    }
}

public sealed record CandidateWorkflowDecision(
    bool CanProceed,
    bool IsRejected,
    string Code,
    string Reason)
{
    public static CandidateWorkflowDecision Allowed(string code, string reason)
        => new(true, false, code, reason);

    public static CandidateWorkflowDecision Blocked(string code, string reason)
        => new(false, false, code, reason);

    public static CandidateWorkflowDecision Rejected(string code, string reason)
        => new(false, true, code, reason);
}
