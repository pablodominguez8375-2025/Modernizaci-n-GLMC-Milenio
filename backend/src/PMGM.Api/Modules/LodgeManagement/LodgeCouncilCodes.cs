namespace PMGM.Api.Modules.LodgeManagement;

public static class LodgeCouncilCodes
{
    public static class SessionStatus
    {
        public const string Scheduled = "scheduled";
        public const string Held = "held";
        public const string Closed = "closed"; // legado: se lee como realizada
        public const string Cancelled = "cancelled";

        public static bool IsValid(string value)
            => value is Scheduled or Held or Closed or Cancelled;

        public static bool IsHeld(string value) => value is Held or Closed;
    }

    public static class ParticipationType
    {
        public const string Member = "member";
        public const string Guest = "guest";

        public static bool IsValid(string value)
            => value is Member or Guest;
    }

    public static class AttendanceStatus
    {
        public const string Present = "present";
        public const string Absent = "absent";
        public const string Excused = "excused";

        public static bool IsValid(string value)
            => value is Present or Absent or Excused;
    }

    public static class DecisionCategory
    {
        public const string Handover = "handover";
        public const string FinancialControl = "financial_control";
        public const string DuesRelief = "dues_relief";
        public const string BenevolenceAidProposal = "benevolence_aid_proposal";
        public const string BudgetProposal = "budget_proposal";
        public const string InstructionProgramProposal = "instruction_program_proposal";
        public const string OfficerChangeProposal = "officer_change_proposal";
        public const string ForcedWithdrawalProposal = "forced_withdrawal_proposal";
        public const string Other = "other";

        public static bool IsValid(string value)
            => value is Handover or FinancialControl or DuesRelief or BenevolenceAidProposal or
                BudgetProposal or InstructionProgramProposal or OfficerChangeProposal or
                ForcedWithdrawalProposal or Other;
    }

    public static class DecisionOutcome
    {
        public const string Approved = "approved";
        public const string Rejected = "rejected";
        public const string ApprovedForReferral = "approved_for_referral";
        public const string Recorded = "recorded";

        public static bool IsValid(string value)
            => value is Approved or Rejected or ApprovedForReferral or Recorded;
    }

    public static class ControlArea
    {
        public const string Treasury = "treasury";
        public const string Hospitalaria = "hospitalaria";
        public const string InstructionColumns = "instruction_columns";

        public static bool IsValid(string value)
            => value is Treasury or Hospitalaria or InstructionColumns;
    }
}
