namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidateIntakeCodes
{
    public static class ReviewStatus
    {
        public const string Pending = "pending_grand_secretariat";
        public const string Observed = "observed";
        public const string Rejected = "rejected";
        public const string Approved = "approved";

        public static bool IsValid(string value)
            => value is Pending or Observed or Rejected or Approved;
    }

    public static class PhotoContentType
    {
        public static bool IsAllowed(string value)
            => value is "image/jpeg" or "image/png";
    }
}
