namespace PMGM.Api.Modules.Membership;

public static class InstitutionalStatusPolicy
{
    public static bool IsPastActive(string? institutionalStatus)
        => Is(institutionalStatus, MembershipCodes.InstitutionalStatus.PastActive);

    public static bool IsVoluntaryWithdrawal(string? institutionalStatus)
        => Is(institutionalStatus, MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal);

    public static string ResolveCurrentStatus(string? latestEventType, bool hasCurrentMembership)
    {
        if (Is(latestEventType, MembershipCodes.InstitutionalStatus.WorkshopTransfer))
            return hasCurrentMembership ? MembershipCodes.InstitutionalStatus.Active : MembershipCodes.InstitutionalStatus.Inactive;
        if (string.IsNullOrWhiteSpace(latestEventType))
            return hasCurrentMembership ? MembershipCodes.InstitutionalStatus.Active : MembershipCodes.InstitutionalStatus.Inactive;
        return latestEventType;
    }

    public static bool KeepsWorkshopRosterMembership(string? institutionalStatus)
        => !Is(institutionalStatus, MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal) &&
           !Is(institutionalStatus, MembershipCodes.InstitutionalStatus.ForcedWithdrawal) &&
           !Is(institutionalStatus, MembershipCodes.InstitutionalStatus.Deceased);

    public static bool IsOperationallyActive(string? institutionalStatus)
        => Is(institutionalStatus, MembershipCodes.InstitutionalStatus.Active) ||
           Is(institutionalStatus, MembershipCodes.InstitutionalStatus.Reinstated) ||
           Is(institutionalStatus, MembershipCodes.InstitutionalStatus.WorkshopTransfer);

    public static bool GeneratesOrdinaryDues(string? institutionalStatus, bool hasActiveMembership)
        => hasActiveMembership && IsOperationallyActive(institutionalStatus);

    private static bool Is(string? value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
