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

        public static int? ToNumeric(string value)
            => value switch
            {
                Apprentice => 1,
                Fellowcraft => 2,
                Master => 3,
                _ => null
            };
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

    public static class BallotType
    {
        public const string WhiteBlack = "white_black";
        public const string PositiveNegative = "positive_negative";
        public const string Candidate = "candidate";
        public static bool IsValid(string value) => value is WhiteBlack or PositiveNegative or Candidate;
    }

    public static class BallotStatus
    {
        public const string Closed = "closed";
        public const string Superseded = "superseded";
    }

    public static class InstructionStatus
    {
        public const string Scheduled = "scheduled";
        public const string Held = "held";
        public const string Cancelled = "cancelled";
    }

    public static class InstructionAttendanceStatus
    {
        public const string Present = "present";
        public const string Absent = "absent";

        public static bool IsValid(string value)
            => value is Present or Absent;
    }

    public static class InstructionOffice
    {
        public const string SecondWarden = "second_warden";
        public const string FirstWarden = "first_warden";
        public const string ImmediatePastMaster = "immediate_past_master";

        public static string? ForGrade(string grade)
            => grade switch
            {
                Grade.Apprentice => SecondWarden,
                Grade.Fellowcraft => FirstWarden,
                Grade.Master => ImmediatePastMaster,
                _ => null
            };
    }
}
