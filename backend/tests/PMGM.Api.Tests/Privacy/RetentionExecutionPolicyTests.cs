using PMGM.Api.Modules.Privacy;
using PMGM.Api.Modules.Privacy.Entities;
using Xunit;

namespace PMGM.Api.Tests.Privacy;

public sealed class RetentionExecutionPolicyTests
{
    [Fact]
    public void ActiveHold_BlocksExecution()
    {
        var evaluation = CreateEvaluation(PrivacyCodes.RetentionAction.Anonymize, nameof(DataSubjectRequest));

        var decision = RetentionExecutionPolicy.Evaluate(
            evaluation,
            PrivacyCodes.RetentionAction.Anonymize,
            hasActiveHold: true);

        Assert.False(decision.CanExecute);
        Assert.False(decision.RequiresManualReview);
    }

    [Fact]
    public void HardDelete_RequiresManualReview()
    {
        var evaluation = CreateEvaluation(PrivacyCodes.RetentionAction.Delete, nameof(DataSubjectRequest));

        var decision = RetentionExecutionPolicy.Evaluate(
            evaluation,
            PrivacyCodes.RetentionAction.Delete,
            hasActiveHold: false);

        Assert.False(decision.CanExecute);
        Assert.True(decision.RequiresManualReview);
    }

    [Fact]
    public void DataSubjectRequest_Anonymization_IsExecutable()
    {
        var evaluation = CreateEvaluation(PrivacyCodes.RetentionAction.Anonymize, nameof(DataSubjectRequest));

        var decision = RetentionExecutionPolicy.Evaluate(
            evaluation,
            PrivacyCodes.RetentionAction.Anonymize,
            hasActiveHold: false);

        Assert.True(decision.CanExecute);
        Assert.False(decision.RequiresManualReview);
    }

    [Fact]
    public void UnsupportedEntity_Anonymization_RequiresManualReview()
    {
        var evaluation = CreateEvaluation(PrivacyCodes.RetentionAction.Anonymize, "Membership");

        var decision = RetentionExecutionPolicy.Evaluate(
            evaluation,
            PrivacyCodes.RetentionAction.Anonymize,
            hasActiveHold: false);

        Assert.False(decision.CanExecute);
        Assert.True(decision.RequiresManualReview);
    }

    [Fact]
    public void MismatchedExpectedAction_IsRejected()
    {
        var evaluation = CreateEvaluation(PrivacyCodes.RetentionAction.Anonymize, nameof(DataSubjectRequest));

        var decision = RetentionExecutionPolicy.Evaluate(
            evaluation,
            PrivacyCodes.RetentionAction.Keep,
            hasActiveHold: false);

        Assert.False(decision.CanExecute);
    }

    private static DataRetentionEvaluation CreateEvaluation(string action, string entityType)
        => new()
        {
            RetentionPolicyId = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = Guid.NewGuid().ToString(),
            AnchorDate = new DateOnly(2025, 1, 1),
            EvaluationDate = new DateOnly(2026, 1, 1),
            DueDate = new DateOnly(2025, 12, 31),
            RecommendedAction = action,
            BlockedByHold = false,
            Rationale = "test",
            Status = RetentionEvaluationStatuses.Ready
        };
}
