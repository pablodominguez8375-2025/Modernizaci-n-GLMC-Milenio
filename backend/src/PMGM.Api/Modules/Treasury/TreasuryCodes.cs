namespace PMGM.Api.Modules.Treasury;

public static class TreasuryCodes
{
    public static class LodgeFeeType
    {
        public const string Normal = "normal";
        public const string Student = "student";
        public const string Senior = "senior";
        public const string Spouse = "spouse";
        public const string PastActive = "past_active";
        public static bool IsValid(string value) => value is Normal or Student or Senior or Spouse or PastActive;
    }

    public static class LodgeChargeStatus
    {
        public const string Pending = "pending";
        public const string Partial = "partial";
        public const string Paid = "paid";
    }

    public static class LodgePaymentMethod
    {
        public const string Cash = "cash";
        public const string Transfer = "transfer";
        public const string Deposit = "deposit";
        public static bool IsValid(string value) => value is Cash or Transfer or Deposit;
    }
    public static class StatementStatus
    {
        public const string Draft = "draft";
        public const string Submitted = "submitted";
        public const string Observed = "observed";
        public const string Reconciled = "reconciled";
        public const string Closed = "closed";
        public const string Rectified = "rectified";
    }

    public static class PaymentMethod
    {
        public const string Transfer = "transfer";
        public const string Deposit = "deposit";

        public static bool IsValid(string value) => value is Transfer or Deposit;
    }

    public static class IdentityMatchStatus
    {
        public const string Matched = "matched";
        public const string Pending = "pending";
        public const string Observed = "observed";

        public static bool IsValid(string value) => value is Matched or Pending or Observed;
    }

    public static class AdjustmentStatus
    {
        public const string Active = "active";
        public const string Expired = "expired";
        public const string Revoked = "revoked";
    }

    public static class Degree
    {
        public const string Apprentice = "apprentice";
        public const string Fellowcraft = "fellowcraft";
        public const string Master = "master";
    }

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
