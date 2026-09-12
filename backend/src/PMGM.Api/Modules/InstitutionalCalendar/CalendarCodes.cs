namespace PMGM.Api.Modules.InstitutionalCalendar;

public static class CalendarCodes
{
    public static class ScopeType
    {
        public const string Order = "order";
        public const string GrandLodge = "grand_lodge";
        public const string Lodge = "lodge";
        public const string Group = "group";
        public const string PrivateAdministrative = "private_admin";

        public static bool IsValid(string value)
            => value is Order or GrandLodge or Lodge or Group or PrivateAdministrative;
    }

    public static class Visibility
    {
        public const string Public = "public";
        public const string Institutional = "institutional";
        public const string Lodge = "lodge";
        public const string Restricted = "restricted";

        public static bool IsValid(string value)
            => value is Public or Institutional or Lodge or Restricted;
    }

    public static class Status
    {
        public const string Draft = "draft";
        public const string Tentative = "tentative";
        public const string Confirmed = "confirmed";
        public const string Cancelled = "cancelled";
        public const string Completed = "completed";

        public static bool IsValid(string value)
            => value is Draft or Tentative or Confirmed or Cancelled or Completed;
    }
}
