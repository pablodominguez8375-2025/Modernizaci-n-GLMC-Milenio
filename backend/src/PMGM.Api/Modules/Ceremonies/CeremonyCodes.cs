namespace PMGM.Api.Modules.Ceremonies;

public static class CeremonyCodes
{
    public static class Type
    {
        public const string Initiation = "initiation";
        public const string WageIncrease = "wage_increase";
        public const string Exaltation = "exaltation";
    }

    public static class RequestStatus
    {
        public const string Draft = "draft";
        public const string UnderReview = "under_review";
        public const string Eligible = "eligible";
        public const string Observed = "observed";
        public const string Rejected = "rejected";
        public const string Authorized = "authorized";
    }

    public static class ValidationType
    {
        public const string InternalAffairs = "internal_affairs";
        public const string Treasury = "treasury";
        public const string Hospitalaria = "hospitalaria";
        public const string CandidatePublication = "candidate_publication";
        public const string SpaceAvailability = "space_availability";
    }

    public static class ValidationStatus
    {
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Observed = "observed";
        public const string Rejected = "rejected";
        public const string NotApplicable = "not_applicable";
        public const string ExceptionApproved = "exception_approved";
    }

    public static class PublicationStatus
    {
        public const string Scheduled = "scheduled";
        public const string Published = "published";
        public const string Suspended = "suspended";
        public const string Completed = "completed";
        public const string Cancelled = "cancelled";
    }

    public static class Rules
    {
        public const string InitiationPublicationMinimumDays = "initiation.publication.minimum_days";
    }
}
