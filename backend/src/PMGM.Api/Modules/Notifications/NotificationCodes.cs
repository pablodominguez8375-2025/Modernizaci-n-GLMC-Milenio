namespace PMGM.Api.Modules.Notifications;

public static class NotificationCodes
{
    public static class Channel
    {
        public const string Internal = "internal";
        public const string Email = "email";

        public static bool IsValid(string value)
            => value is Internal or Email;
    }

    public static class DeliveryStatus
    {
        public const string Queued = "queued";
        public const string Delivered = "delivered";
        public const string Failed = "failed";
        public const string DeadLetter = "dead_letter";

        public static bool IsValid(string value)
            => value is Queued or Delivered or Failed or DeadLetter;
    }

    public static class TemplateStatus
    {
        public const string Active = "active";
        public const string Inactive = "inactive";

        public static bool IsValid(string value)
            => value is Active or Inactive;
    }

    public static class Classification
    {
        public const string Internal = "internal";
        public const string Confidential = "confidential";
        public const string Restricted = "restricted";

        public static bool IsValid(string value)
            => value is Internal or Confidential or Restricted;
    }
}
