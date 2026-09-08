namespace PMGM.Api.Modules.LodgeManagement;

public static class LodgeManagementCodes
{
    public static class MeetingType
    {
        public const string Regular = "regular";
        public const string Solemn = "solemn";
        public const string Instruction = "instruction";
        public const string Anniversary = "anniversary";
        public const string Funeral = "funeral";
        public const string Special = "special";

        public static bool IsValid(string value)
            => value is Regular or Solemn or Instruction or Anniversary or Funeral or Special;
    }

    public static class Grade
    {
        public const string Apprentice = "apprentice";
        public const string Fellowcraft = "fellowcraft";
        public const string Master = "master";
        public const string All = "all";

        public static bool IsValid(string value)
            => value is Apprentice or Fellowcraft or Master or All;
    }

    public static class MeetingStatus
    {
        public const string Scheduled = "scheduled";
        public const string Open = "open";
        public const string Closed = "closed";
        public const string Cancelled = "cancelled";
    }

    public static class AttendanceStatus
    {
        public const string Present = "present";
        public const string Excused = "excused";
        public const string Absent = "absent";

        public static bool IsValid(string value)
            => value is Present or Excused or Absent;
    }

    public static class MinuteStatus
    {
        public const string Draft = "draft";
        public const string Approved = "approved";
        public const string Superseded = "superseded";
    }
}
