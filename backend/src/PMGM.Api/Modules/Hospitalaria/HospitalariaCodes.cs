namespace PMGM.Api.Modules.Hospitalaria;

public static class HospitalariaCodes
{
    public static class ReplenishmentStatus
    {
        public const string Pending = "pending";
        public const string Partial = "partial";
        public const string Paid = "paid";
        public const string Submitted = "submitted";
        public const string Reconciled = "reconciled";
        public const string Observed = "observed";
    }

    public static class SubmissionStatus
    {
        public const string Draft = "draft";
        public const string Submitted = "submitted";
        public const string Observed = "observed";
        public const string Reconciled = "reconciled";

        public static bool IsValid(string value)
            => value is Draft or Submitted or Observed or Reconciled;
    }

    public static class ReviewDecision
    {
        public const string Observed = "observed";
        public const string Reconciled = "reconciled";

        public static bool IsValid(string value)
            => value is Observed or Reconciled;
    }

    public static class ApprovalSource
    {
        public const string VenerableMaster = "venerable_master";
        public const string LodgeCouncil = "lodge_council";
    }

    public static class RegularityStatus
    {
        public const string UpToDate = "up_to_date";
        public const string Overdue = "overdue";
        public const string Pending = "pending";
        public const string Exempt = "exempt";

        public static bool IsValid(string value)
            => value is UpToDate or Overdue or Pending or Exempt;
    }
}
