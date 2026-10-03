using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;
using MembershipEntity = PMGM.Api.Modules.Membership.Entities.Membership;

namespace PMGM.Api.Tests.Admissions;

public sealed class TransferWithdrawalPolicyTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("active", false)]
    [InlineData("forced", false)]
    [InlineData("unapproved", false)]
    [InlineData("unsigned", false)]
    [InlineData("missing_timestamp", false)]
    [InlineData("wrong_member", false)]
    [InlineData("wrong_lodge", false)]
    [InlineData("different_closure", false)]
    [InlineData("same_day", false)]
    [InlineData("before", false)]
    [InlineData("missing", false)]
    public void Origin_cycle_must_be_closed_by_the_same_signed_voluntary_withdrawal(string scenario, bool expected)
    {
        var date = new DateOnly(2026, 10, 1);
        var origin = new MembershipEntity { MemberId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(),
            MembershipType = "regular", Status = "closed", StartDate = date.AddYears(-1), EndDate = date };
        var letter = new MemberWithdrawalRequest { MemberId = origin.MemberId, OriginOrganizationId = origin.OrganizationId,
            WithdrawalType = "voluntary", RequestedEffectiveDate = date, Status = "approved", Reason = "SYNTHETIC",
            EvidenceReference = "SYNTHETIC", OratorSignatureSubject = "synthetic-orator", OratorSignedAtUtc = DateTimeOffset.UtcNow };
        var destination = date.AddDays(1);
        switch (scenario)
        {
            case "active": origin.Status = "active"; origin.EndDate = null; break;
            case "forced": letter.WithdrawalType = "forced"; break;
            case "unapproved": letter.Status = "pending"; break;
            case "unsigned": letter.OratorSignatureSubject = null; break;
            case "missing_timestamp": letter.OratorSignedAtUtc = null; break;
            case "wrong_member": letter.MemberId = Guid.NewGuid(); break;
            case "wrong_lodge": letter.OriginOrganizationId = Guid.NewGuid(); break;
            case "different_closure": origin.EndDate = date.AddDays(-1); break;
            case "same_day": destination = date; break;
            case "before": destination = date.AddDays(-1); break;
        }
        Assert.Equal(expected, TransferWithdrawalPolicy.Allows(origin, scenario == "missing" ? null : letter, destination));
        Assert.Equal(scenario == "different_closure" ? date.AddDays(-1) : scenario == "active" ? null : date, origin.EndDate);
    }
}
