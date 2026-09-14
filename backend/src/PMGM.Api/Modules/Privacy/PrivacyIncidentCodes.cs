using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyIncidentCodes
{
    public static class Status
    {
        public const string Open = "open";
        public const string Assessed = "assessed";
        public const string NotificationPending = "notification_pending";
        public const string Mitigating = "mitigating";
        public const string Closed = "closed";
    }

    public static class NotificationTarget
    {
        public const string Authority = "authority";
        public const string Subjects = "subjects";

        public static bool IsValid(string value)
            => value is Authority or Subjects;
    }

    public static class NotificationDecision
    {
        public const string Notify = "notify";
        public const string DoNotNotify = "do_not_notify";

        public static bool IsValid(string value)
            => value is Notify or DoNotNotify;
    }
}

public static class PrivacyIncidentWorkflowPolicy
{
    public static IncidentClosureDecision EvaluateClosure(PrivacySecurityIncident incident)
    {
        var blockers = new List<string>();

        if (incident.AssessmentCompletedAtUtc is null)
        {
            blockers.Add("El incidente aún no tiene evaluación de riesgo formal registrada.");
        }

        if (!PrivacyIncidentCodes.NotificationDecision.IsValid(incident.AuthorityDecision ?? string.Empty))
        {
            blockers.Add("Debe registrarse una decisión explícita respecto de notificación a la autoridad.");
        }
        else if (incident.AuthorityDecision == PrivacyIncidentCodes.NotificationDecision.Notify &&
                 incident.AuthorityNotifiedAtUtc is null)
        {
            blockers.Add("La decisión exige notificar a la autoridad, pero no existe comunicación registrada.");
        }

        if (!PrivacyIncidentCodes.NotificationDecision.IsValid(incident.SubjectsDecision ?? string.Empty))
        {
            blockers.Add("Debe registrarse una decisión explícita respecto de comunicación a los titulares.");
        }
        else if (incident.SubjectsDecision == PrivacyIncidentCodes.NotificationDecision.Notify &&
                 incident.SubjectsNotifiedAtUtc is null)
        {
            blockers.Add("La decisión exige comunicar a los titulares, pero no existe comunicación registrada.");
        }

        if (string.IsNullOrWhiteSpace(incident.CorrectiveActions))
        {
            blockers.Add("Deben documentarse las medidas correctivas o de cierre antes de cerrar el incidente.");
        }

        return new IncidentClosureDecision(blockers.Count == 0, blockers);
    }
}

public sealed record IncidentClosureDecision(
    bool CanClose,
    IReadOnlyList<string> BlockingReasons);
