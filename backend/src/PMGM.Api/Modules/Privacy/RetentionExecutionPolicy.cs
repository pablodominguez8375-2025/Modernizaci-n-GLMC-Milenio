using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class RetentionExecutionStatuses
{
    public const string Executed = "executed";
}

public sealed record RetentionExecutionDecision(
    bool CanExecute,
    bool RequiresManualReview,
    string Reason);

public static class RetentionExecutionPolicy
{
    public static RetentionExecutionDecision Evaluate(
        DataRetentionEvaluation evaluation,
        string expectedAction,
        bool hasActiveHold)
    {
        if (evaluation.Status == RetentionExecutionStatuses.Executed || evaluation.ExecutedAtUtc is not null)
        {
            return new(false, false, "La evaluación de retención ya fue ejecutada.");
        }

        if (evaluation.BlockedByHold || hasActiveHold)
        {
            return new(false, false, "Existe un legal hold vigente o la evaluación fue marcada como bloqueada por hold.");
        }

        if (evaluation.Status != RetentionEvaluationStatuses.Ready)
        {
            return new(false, false, "La evaluación no se encuentra en estado listo para ejecución.");
        }

        if (!PrivacyCodes.RetentionAction.IsValid(expectedAction) || evaluation.RecommendedAction != expectedAction)
        {
            return new(false, false, "La acción esperada no coincide con la acción recomendada por la evaluación vigente.");
        }

        if (expectedAction == PrivacyCodes.RetentionAction.Review)
        {
            return new(false, true, "La política exige revisión humana; no corresponde una ejecución automática.");
        }

        if (expectedAction == PrivacyCodes.RetentionAction.Delete)
        {
            return new(false, true, "El borrado duro no está habilitado para ejecución automática en esta fase.");
        }

        if (expectedAction == PrivacyCodes.RetentionAction.Anonymize &&
            evaluation.EntityType != nameof(DataSubjectRequest))
        {
            return new(false, true, "La anonimización automática aún no está habilitada para este tipo de entidad.");
        }

        return new(true, false, "La evaluación puede ejecutarse con los controles actuales.");
    }
}
