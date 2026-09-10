using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Treasury;

public static class MemberTreasuryPolicy
{
    public static bool BlocksOrdinaryDues(string? institutionalStatus)
        => institutionalStatus is MembershipCodes.InstitutionalStatus.PastActive
            or MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal
            or MembershipCodes.InstitutionalStatus.ForcedWithdrawal
            or MembershipCodes.InstitutionalStatus.Deceased
            or MembershipCodes.InstitutionalStatus.Inactive;
}
