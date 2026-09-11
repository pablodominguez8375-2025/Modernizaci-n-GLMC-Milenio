namespace PMGM.Api.Modules.AssemblyGovernance;

public static class AssemblyGovernanceCodes
{
    public static class AssemblyStatus
    {
        public const string Draft = "draft";
        public const string RosterOpen = "roster_open";
        public const string Frozen = "frozen";
        public const string Closed = "closed";
    }

    public static class MemberCategory
    {
        public const string FormerWorshipfulMaster = "former_worshipful_master";
        public const string LodgeRepresentative = "lodge_representative";
    }

    public static class EligibilityStatus
    {
        public const string Pending = "pending";
        public const string Enabled = "enabled";
        public const string Disabled = "disabled";
    }

    public static class RestrictionOrigin
    {
        public const string InternalAffairs = "internal_affairs";
        public const string HonorTribunal = "honor_tribunal";
        public const string Treasury = "treasury";
        public const string Other = "other";
    }

    public static class Reason
    {
        public const string NotAssemblyMember = "not_assembly_member";
        public const string LodgeTreasuryOverdue = "lodge_treasury_overdue";
        public const string AttendanceRestricted = "attendance_restricted";
        public const string VotingRestricted = "voting_restricted";
        public const string MasonicRightsSuspended = "masonic_rights_suspended";
    }
}
