namespace PMGM.Api.Modules.SecretariatOperations;

public static class SecretariatOperationsCodes
{
    public static class IntakeStatus
    {
        public const string Draft = "draft";
        public const string Submitted = "submitted";
        public const string Observed = "observed";
        public const string Approved = "approved";
        public const string Rejected = "rejected";

        public static bool IsEditable(string value) => value is Draft or Observed;
    }

    public static class CurrentDegree
    {
        public const string Apprentice = "apprentice";
        public const string Fellowcraft = "fellowcraft";
        public const string Master = "master";
        public static bool IsValid(string value) => value is Apprentice or Fellowcraft or Master;
    }

    public static class AdministrativeMeetingStatus
    {
        public const string Scheduled = "scheduled";
        public const string Held = "held";
        public const string Cancelled = "cancelled";
    }

    public static class RecordType
    {
        public const string LodgeMeeting = "tenida";
        public const string AdministrativeMeeting = "reunion";
        public const string Council = "consejo";
        public static bool IsValid(string value) => value is LodgeMeeting or AdministrativeMeeting or Council;
    }

    public static class SubmissionStatus
    {
        public const string Draft = "draft";
        public const string Submitted = "submitted";
        public const string Received = "received";
        public const string Observed = "observed";
    }

    public static class CeremonyType
    {
        public const string Initiation = "initiation";
        public const string WageIncrease = "wage_increase";
        public const string Exaltation = "exaltation";
        public static bool IsValid(string? value)
            => value is null or Initiation or WageIncrease or Exaltation;
        public static bool IsCeremonial(string? value)
            => value is Initiation or WageIncrease or Exaltation;
    }

    public static class Correspondence
    {
        public static bool IsDirection(string value) => value is "received" or "sent";
        public static bool IsChannel(string value) => value is "email" or "letter" or "hand_delivery" or "other";
        public static bool IsStatus(string value) => value is "registered" or "closed";
    }

    public static class Task
    {
        public static bool IsPriority(string value) => value is "low" or "normal" or "high" or "urgent";
        public static bool IsStatus(string value) => value is "pending" or "in_progress" or "completed" or "cancelled";
    }

    public static class Agenda
    {
        public static bool IsStatus(string value) => value is "pending" or "covered" or "deferred";
    }
}
