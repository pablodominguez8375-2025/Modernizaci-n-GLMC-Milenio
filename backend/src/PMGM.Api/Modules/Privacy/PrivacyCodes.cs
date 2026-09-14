namespace PMGM.Api.Modules.Privacy;

public static class PrivacyCodes
{
    public static class Status
    {
        public const string Draft = "draft";
        public const string Active = "active";
        public const string UnderReview = "under_review";
        public const string Approved = "approved";
        public const string Closed = "closed";
        public const string Suspended = "suspended";
        public const string Retired = "retired";
    }

    public static class DataSubjectRight
    {
        public const string Access = "access";
        public const string Rectification = "rectification";
        public const string Erasure = "erasure";
        public const string Objection = "objection";
        public const string Portability = "portability";
        public const string Blocking = "blocking";

        public static bool IsValid(string value)
            => value is Access or Rectification or Erasure or Objection or Portability or Blocking;
    }

    public static class RetentionAction
    {
        public const string Keep = "keep";
        public const string Review = "review";
        public const string Anonymize = "anonymize";
        public const string Delete = "delete";

        public static bool IsValid(string value)
            => value is Keep or Review or Anonymize or Delete;
    }

    public static class RiskLevel
    {
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";
        public const string Critical = "critical";
    }
}
