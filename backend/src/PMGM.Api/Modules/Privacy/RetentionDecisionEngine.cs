using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class RetentionEvaluationStatuses
{
    public const string Ready = "ready";
    public const string BlockedByHold = "blocked_by_hold";
}

public sealed record RetentionDecision(
    DateOnly? DueDate,
    string RecommendedAction,
    bool BlockedByHold,
    string Rationale);

public static class RetentionDecisionEngine
{
    public static RetentionDecision Evaluate(
        DataRetentionPolicy policy,
        DateOnly anchorDate,
        DateOnly evaluationDate,
        bool hasActiveHold)
    {
        if (policy.RetentionDays is null || policy.RetentionDays < 0)
        {
            return new RetentionDecision(
                null,
                PrivacyCodes.RetentionAction.Review,
                false,
                "La política no define un plazo de conservación calculable en días; requiere revisión humana antes de ejecutar una acción.");
        }

        var dueDate = anchorDate.AddDays(policy.RetentionDays.Value);
        if (evaluationDate < dueDate)
        {
            return new RetentionDecision(
                dueDate,
                PrivacyCodes.RetentionAction.Keep,
                false,
                $"El plazo de conservación vence el {dueDate:yyyy-MM-dd}; todavía no corresponde ejecutar la acción de expiración.");
        }

        var action = PrivacyCodes.RetentionAction.IsValid(policy.ExpirationAction)
            ? policy.ExpirationAction
            : PrivacyCodes.RetentionAction.Review;

        if (hasActiveHold)
        {
            return new RetentionDecision(
                dueDate,
                action,
                true,
                "El plazo de conservación venció, pero existe un legal hold vigente. La acción queda bloqueada hasta la liberación formal del hold.");
        }

        return new RetentionDecision(
            dueDate,
            action,
            false,
            $"El plazo de conservación venció el {dueDate:yyyy-MM-dd}; corresponde aplicar la acción definida por la política.");
    }
}
