namespace PMGM.Api.Modules.Hospitalaria;

public static class HospitalariaCodes
{
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
