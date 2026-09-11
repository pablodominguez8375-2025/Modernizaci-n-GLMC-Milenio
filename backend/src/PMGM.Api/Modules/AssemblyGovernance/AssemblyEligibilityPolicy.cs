namespace PMGM.Api.Modules.AssemblyGovernance;

public sealed record AssemblyEligibilityInput(
    bool IsAssemblyMember,
    bool LodgeTreasuryUpToDate,
    bool HasAttendanceRestriction,
    bool HasVotingRestriction,
    bool HasMasonicRights);

public sealed record AssemblyEligibilityDecision(
    bool CanAttend,
    bool CanVote,
    string Status,
    string? PrimaryReason,
    IReadOnlyList<string> Reasons);

public static class AssemblyEligibilityPolicy
{
    public static AssemblyEligibilityDecision Evaluate(AssemblyEligibilityInput input)
    {
        var reasons = new List<string>();

        if (!input.IsAssemblyMember)
            reasons.Add(AssemblyGovernanceCodes.Reason.NotAssemblyMember);
        if (!input.LodgeTreasuryUpToDate)
            reasons.Add(AssemblyGovernanceCodes.Reason.LodgeTreasuryOverdue);
        if (input.HasAttendanceRestriction)
            reasons.Add(AssemblyGovernanceCodes.Reason.AttendanceRestricted);
        if (input.HasVotingRestriction)
            reasons.Add(AssemblyGovernanceCodes.Reason.VotingRestricted);
        if (!input.HasMasonicRights)
            reasons.Add(AssemblyGovernanceCodes.Reason.MasonicRightsSuspended);

        var canAttend = input.IsAssemblyMember &&
                        input.HasMasonicRights &&
                        !input.HasAttendanceRestriction;

        var canVote = input.IsAssemblyMember &&
                      input.HasMasonicRights &&
                      input.LodgeTreasuryUpToDate &&
                      !input.HasVotingRestriction;

        var fullyEnabled = canAttend && canVote;
        return new AssemblyEligibilityDecision(
            canAttend,
            canVote,
            fullyEnabled
                ? AssemblyGovernanceCodes.EligibilityStatus.Enabled
                : AssemblyGovernanceCodes.EligibilityStatus.Disabled,
            reasons.FirstOrDefault(),
            reasons);
    }
}
