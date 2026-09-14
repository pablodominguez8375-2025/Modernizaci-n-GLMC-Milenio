using PMGM.Api.Modules.Privacy;
using PMGM.Api.Modules.Privacy.Entities;
using Xunit;

namespace PMGM.Api.Tests.Privacy;

public sealed class PrivacyIncidentWorkflowPolicyTests
{
    [Fact]
    public void Closure_IsBlocked_WhenAssessmentDecisionsOrCorrectiveActionsAreMissing()
    {
        var incident = CreateIncident();

        var decision = PrivacyIncidentWorkflowPolicy.EvaluateClosure(incident);

        Assert.False(decision.CanClose);
        Assert.NotEmpty(decision.BlockingReasons);
    }

    [Fact]
    public void Closure_IsAllowed_WhenBothDecisionsAreExplicitAndNoNotificationsAreRequired()
    {
        var incident = CreateIncident();
        incident.AssessmentCompletedAtUtc = DateTimeOffset.UtcNow;
        incident.AuthorityDecision = PrivacyIncidentCodes.NotificationDecision.DoNotNotify;
        incident.SubjectsDecision = PrivacyIncidentCodes.NotificationDecision.DoNotNotify;
        incident.CorrectiveActions = "Medidas correctivas documentadas.";

        var decision = PrivacyIncidentWorkflowPolicy.EvaluateClosure(incident);

        Assert.True(decision.CanClose);
        Assert.Empty(decision.BlockingReasons);
    }

    [Fact]
    public void Closure_IsBlocked_WhenAuthorityNotificationWasRequiredButNotRecorded()
    {
        var incident = CreateIncident();
        incident.AssessmentCompletedAtUtc = DateTimeOffset.UtcNow;
        incident.AuthorityDecision = PrivacyIncidentCodes.NotificationDecision.Notify;
        incident.SubjectsDecision = PrivacyIncidentCodes.NotificationDecision.DoNotNotify;
        incident.CorrectiveActions = "Medidas correctivas documentadas.";

        var decision = PrivacyIncidentWorkflowPolicy.EvaluateClosure(incident);

        Assert.False(decision.CanClose);
        Assert.Contains(decision.BlockingReasons, x => x.Contains("autoridad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Closure_IsAllowed_WhenRequiredCommunicationsWereRecorded()
    {
        var incident = CreateIncident();
        incident.AssessmentCompletedAtUtc = DateTimeOffset.UtcNow;
        incident.AuthorityDecision = PrivacyIncidentCodes.NotificationDecision.Notify;
        incident.AuthorityNotifiedAtUtc = DateTimeOffset.UtcNow;
        incident.SubjectsDecision = PrivacyIncidentCodes.NotificationDecision.Notify;
        incident.SubjectsNotifiedAtUtc = DateTimeOffset.UtcNow;
        incident.CorrectiveActions = "Medidas correctivas documentadas.";

        var decision = PrivacyIncidentWorkflowPolicy.EvaluateClosure(incident);

        Assert.True(decision.CanClose);
    }

    private static PrivacySecurityIncident CreateIncident()
        => new()
        {
            Source = "test",
            Nature = "test incident",
            DataCategoriesJson = "[]",
            RiskLevel = PrivacyCodes.RiskLevel.Medium,
            Status = PrivacyIncidentCodes.Status.Open
        };
}
