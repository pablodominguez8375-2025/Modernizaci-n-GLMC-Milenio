using PMGM.Api.Modules.Membership.Entities;
using MembershipEntity = PMGM.Api.Modules.Membership.Entities.Membership;

namespace PMGM.Api.Modules.Membership;

public static class TransferWithdrawalPolicy
{
    public static bool Allows(MembershipEntity origin, MemberWithdrawalRequest? withdrawal, DateOnly destinationDate)
        => withdrawal is not null && withdrawal.MemberId == origin.MemberId &&
           withdrawal.OriginOrganizationId == origin.OrganizationId &&
           withdrawal.WithdrawalType == MembershipCodes.WithdrawalType.Voluntary &&
           withdrawal.Status == MembershipCodes.WithdrawalRequestStatus.Approved &&
           !string.IsNullOrWhiteSpace(withdrawal.OratorSignatureSubject) && withdrawal.OratorSignedAtUtc is not null &&
           origin.Status == MembershipCodes.MembershipStatus.Closed && origin.EndDate == withdrawal.RequestedEffectiveDate &&
           destinationDate > withdrawal.RequestedEffectiveDate;
}
