namespace PMGM.Api.Modules.Membership;

public static class MembershipCodes
{
    public static class MembershipStatus
    {
        public const string Active = "active";
        public const string Transferred = "transferred";
        public const string Closed = "closed";
    }

    public static class TransferStatus
    {
        public const string Requested = "requested";
        public const string Approved = "approved";
        public const string Executed = "executed";
        public const string Rejected = "rejected";
        public const string Cancelled = "cancelled";
    }

    public static class InstitutionalStatus
    {
        public const string Active = "active";
        public const string Inactive = "inactive";
        public const string PastActive = "past_active";
        public const string VoluntaryWithdrawal = "voluntary_withdrawal";
        public const string ForcedWithdrawal = "forced_withdrawal";
        public const string Reinstated = "reinstated";
        public const string Deceased = "deceased";
        public const string WorkshopTransfer = "workshop_transfer";
    }

    public static class DegreeEvent
    {
        public const string Initiation = "initiation";
        public const string WageIncrease = "wage_increase";
        public const string Exaltation = "exaltation";
    }
}
