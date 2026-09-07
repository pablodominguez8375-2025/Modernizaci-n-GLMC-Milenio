using PMGM.Api.Modules.Privacy;
using PMGM.Api.Modules.Privacy.Entities;
using Xunit;

namespace PMGM.Api.Tests.Privacy;

public sealed class RetentionDecisionEngineTests
{
    [Fact]
    public void Evaluate_BeforeDueDate_KeepsData()
    {
        var policy = CreatePolicy(30, PrivacyCodes.RetentionAction.Delete);

        var result = RetentionDecisionEngine.Evaluate(
            policy,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 15),
            hasActiveHold: false);

        Assert.Equal(new DateOnly(2026, 10, 1), result.DueDate);
        Assert.Equal(PrivacyCodes.RetentionAction.Keep, result.RecommendedAction);
        Assert.False(result.BlockedByHold);
    }

    [Fact]
    public void Evaluate_AfterDueDate_UsesPolicyAction()
    {
        var policy = CreatePolicy(30, PrivacyCodes.RetentionAction.Delete);

        var result = RetentionDecisionEngine.Evaluate(
            policy,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 7),
            hasActiveHold: false);

        Assert.Equal(new DateOnly(2026, 8, 31), result.DueDate);
        Assert.Equal(PrivacyCodes.RetentionAction.Delete, result.RecommendedAction);
        Assert.False(result.BlockedByHold);
    }

    [Fact]
    public void Evaluate_AfterDueDate_WithActiveHold_BlocksExecution()
    {
        var policy = CreatePolicy(30, PrivacyCodes.RetentionAction.Anonymize);

        var result = RetentionDecisionEngine.Evaluate(
            policy,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 7),
            hasActiveHold: true);

        Assert.Equal(PrivacyCodes.RetentionAction.Anonymize, result.RecommendedAction);
        Assert.True(result.BlockedByHold);
        Assert.Contains("legal hold", result.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WithoutRetentionDays_RequiresHumanReview()
    {
        var policy = CreatePolicy(null, PrivacyCodes.RetentionAction.Delete);

        var result = RetentionDecisionEngine.Evaluate(
            policy,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 7),
            hasActiveHold: false);

        Assert.Null(result.DueDate);
        Assert.Equal(PrivacyCodes.RetentionAction.Review, result.RecommendedAction);
        Assert.False(result.BlockedByHold);
    }

    [Fact]
    public void Evaluate_WithUnknownExpirationAction_FallsBackToReview()
    {
        var policy = CreatePolicy(1, "destroy_without_review");

        var result = RetentionDecisionEngine.Evaluate(
            policy,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 7),
            hasActiveHold: false);

        Assert.Equal(PrivacyCodes.RetentionAction.Review, result.RecommendedAction);
    }

    private static DataRetentionPolicy CreatePolicy(int? retentionDays, string expirationAction)
        => new()
        {
            Code = "RET-TEST",
            Name = "Política de prueba",
            DataCategory = "test",
            Purpose = "Pruebas automatizadas",
            LegalBasis = "Base jurídica de prueba",
            RetentionDays = retentionDays,
            ExpirationAction = expirationAction,
            AllowsLegalHold = true,
            Status = PrivacyCodes.Status.Active,
            EffectiveFrom = new DateOnly(2026, 1, 1)
        };
}
