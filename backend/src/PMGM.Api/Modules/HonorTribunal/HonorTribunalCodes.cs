namespace PMGM.Api.Modules.HonorTribunal;

public static class HonorTribunalCodes
{
    public static class CaseStatus
    {
        public const string Open = "open";
        public const string UnderReview = "under_review";
        public const string Resolved = "resolved";
        public const string Closed = "closed";
    }

    public static class SanctionType
    {
        public const string Warning = "warning";
        public const string TemporaryRightsSuspension = "temporary_rights_suspension";
        public const string AttendanceRestriction = "attendance_restriction";
        public const string VotingRestriction = "voting_restriction";
        public const string TotalLossOfRights = "total_loss_of_rights";
    }
}

public sealed record HonorInstitutionalEffect(
    bool HasMasonicRights,
    bool AttendanceRestricted,
    bool VotingRestricted,
    string? EffectReference);
