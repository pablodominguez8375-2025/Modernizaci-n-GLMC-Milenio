namespace PMGM.Api.Modules.Treasury;

public static class TreasuryCodes
{
    public static class RegularityScope
    {
        public const string Order = "order";
        public const string Organization = "organization";
    }

    public static class RegularityStatus
    {
        public const string UpToDate = "up_to_date";
        public const string Delinquent = "delinquent";
        public const string Pending = "pending";
        public const string Exempt = "exempt";

        public static bool IsValid(string value)
            => value is UpToDate or Delinquent or Pending or Exempt;
    }
}
