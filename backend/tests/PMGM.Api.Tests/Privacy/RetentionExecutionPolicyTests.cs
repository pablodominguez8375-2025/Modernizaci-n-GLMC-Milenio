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

    [Theory]
    [InlineData(PrivacyCodes.Status.UnderReview, false)]
    [InlineData(PrivacyCodes.Status.UnderReview, true)]
    [InlineData(PrivacyCodes.Status.Closed, false)]
    [InlineData(PrivacyCodes.Status.Active, true)]
    [InlineData("unknown", true)]
    public void Anonymization_RejectsOpenOrUncertifiedDataSubjectRequest(
        string status, bool hasClosingDate)
    {
        var request = CreateRequest(status, hasClosingDate);
        var decision = RetentionExecutionPolicy.CanAnonymizeDataSubjectRequest(request);

        Assert.False(decision.CanExecute);
        Assert.True(decision.RequiresManualReview);
        Assert.Contains("cierre formal", decision.Reason);
    }

    [Fact]
    public void Anonymization_AcceptsFormallyClosedDataSubjectRequest()
    {
        var request = CreateRequest(PrivacyCodes.Status.Closed, hasClosingDate: true);
        var decision = RetentionExecutionPolicy.CanAnonymizeDataSubjectRequest(request);

        Assert.True(decision.CanExecute);
        Assert.False(decision.RequiresManualReview);
    }

    private static DataSubjectRequest CreateRequest(string status, bool hasClosingDate)
        => new()
        {
            RequestType = PrivacyCodes.DataSubjectRight.Access,
            Channel = "test",
            Status = status,
            ReceivedAtUtc = DateTimeOffset.UtcNow,
            ClosedAtUtc = hasClosingDate ? DateTimeOffset.UtcNow : null
        };

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
