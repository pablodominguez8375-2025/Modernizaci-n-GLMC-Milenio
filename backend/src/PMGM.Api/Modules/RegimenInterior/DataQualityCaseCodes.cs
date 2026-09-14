namespace PMGM.Api.Modules.RegimenInterior;

public static class DataQualityCaseCodes
{
    public static class Status
    {
        public const string Open = "open";
        public const string UnderReview = "under_review";
        public const string ResolvedConfirmed = "resolved_confirmed";
        public const string Dismissed = "dismissed";

        public static bool IsValid(string value)
            => value is Open or UnderReview or ResolvedConfirmed or Dismissed;

        public static bool IsActive(string value)
            => value is Open or UnderReview;
    }

    public static class Action
    {
        public const string Opened = "opened";
        public const string Claimed = "claimed";
        public const string ResolvedConfirmed = "resolved_confirmed";
        public const string Dismissed = "dismissed";
    }
}
