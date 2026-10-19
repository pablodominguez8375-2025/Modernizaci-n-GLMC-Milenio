using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyVoterEligibilityPolicyTests
{
    private static readonly Guid Lodge = Guid.Parse("a1f5da85-d334-4e44-81ad-11c1cd5dd580");
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static AssemblyGrandTreasuryBalance Balance(decimal amount) =>
        new(Lodge, Cutoff, AssemblyVoterEligibilityPolicy.GrandTreasurySource,
            "GT-CIERRE-19-10", "CLP", amount, true);

    private static AssemblyEligibilityFacts Facts(decimal? amount = 0m) =>
        new(Lodge, Cutoff, true, true, true, true, true, true, true,
            amount is null ? null : Balance(amount.Value), null);

    private static AssemblyDebtException Exception(string authority = "regimen_interior",
        DateOnly? revoked = null) =>
        new(Lodge, authority, "regimen.persona-autorizada", Cutoff,
            "Resolución motivada del órgano competente", "PL-RES-2026-01", revoked);

    [Fact]
    public void Only_exact_zero_verified_debt_allows_suffrage()
    {
        var upToDate = AssemblyVoterEligibilityPolicy.Evaluate(Facts());
        Assert.Equal(AssemblyVoterEligibilityPolicy.Eligible, upToDate.Status);
        Assert.True(upToDate.MayAttend);
        Assert.True(upToDate.MayVote);

        var oneCent = AssemblyVoterEligibilityPolicy.Evaluate(Facts(0.01m));
        Assert.Equal(AssemblyVoterEligibilityPolicy.AttendanceOnly, oneCent.Status);
        Assert.True(oneCent.MayAttend);
        Assert.False(oneCent.MayVote);
        Assert.Equal("MOROSIDAD_GRAN_TESORERIA", oneCent.ReasonCode);
    }

    [Fact]
    public void Missing_stale_foreign_unreconciled_or_member_only_balance_cannot_enable_vote()
    {
        var current = Facts();
        var possibilities = new AssemblyGrandTreasuryBalance?[]
        {
            null,
            Balance(0) with { OrganizationId = Guid.NewGuid() },
            Balance(0) with { AsOfDate = Cutoff.AddDays(-1) },
            Balance(0) with { SourceSystem = "lodge_treasury" },
            Balance(0) with { SourceReference = "" },
            Balance(0) with { Reconciled = false },
            Balance(0) with { Currency = "" },
            Balance(-1m)
        };
        foreach (var evidence in possibilities)
        {
            var result = AssemblyVoterEligibilityPolicy.Evaluate(
                current with { GrandTreasuryBalance = evidence });
            Assert.Equal(AssemblyVoterEligibilityPolicy.Pending, result.Status);
            Assert.False(result.MayVote);
        }
    }

    [Fact]
    public void Only_regimen_interior_reasoned_unrevoked_exception_can_lift_positive_debt()
    {
        var facts = Facts(1000m);
        Assert.False(AssemblyVoterEligibilityPolicy.Evaluate(facts).MayVote);

        var approved = facts with { DebtException = Exception() };
        Assert.True(AssemblyVoterEligibilityPolicy.Evaluate(approved).MayVote);

        var blocked = new AssemblyDebtException[]
        {
            Exception("gran_treasury"),
            Exception("platform_admin"),
            Exception("workshop_secretariat"),
            Exception() with { OrganizationId = Guid.NewGuid() },
            Exception() with { Reason = "" },
            Exception() with { EvidenceReference = "" },
            Exception() with { DecidedBy = "" },
            Exception() with { RevokedAt = Cutoff },
            Exception() with { DecidedAt = Cutoff.AddDays(1) }
        };
        foreach (var exception in blocked)
        {
            Assert.False(AssemblyVoterEligibilityPolicy.Evaluate(
                facts with { DebtException = exception }).MayVote);
        }
    }

    [Theory]
    [InlineData("regimen_interior", true)]
    [InlineData("gran_treasury", false)]
    [InlineData("workshop_secretariat", false)]
    [InlineData("platform_admin", false)]
    [InlineData("regimen_interior_admin", false)]
    public void Debt_exception_authority_is_exclusive(string serverConfirmedRole, bool allowed)
    {
        Assert.Equal(allowed, AssemblyVoterEligibilityPolicy.CanDecideDebtException(serverConfirmedRole));
    }

    [Fact]
    public void Override_never_ignores_missing_financial_evidence_or_separate_rights()
    {
        var noFinancial = Facts(null) with { DebtException = Exception() };
        Assert.Equal(AssemblyVoterEligibilityPolicy.Pending,
            AssemblyVoterEligibilityPolicy.Evaluate(noFinancial).Status);

        var suspended = Facts(100m) with { DebtException = Exception(), HonorAllowsSuffrage = false };
        var decision = AssemblyVoterEligibilityPolicy.Evaluate(suspended);
        Assert.True(decision.MayAttend);
        Assert.False(decision.MayVote);

        var inactive = Facts(100m) with { DebtException = Exception(), MemberActive = false };
        Assert.False(AssemblyVoterEligibilityPolicy.Evaluate(inactive).MayAttend);
        Assert.False(AssemblyVoterEligibilityPolicy.Evaluate(inactive).MayVote);
    }

    [Fact]
    public void Unknown_rights_and_unknown_honor_are_pending_not_eligible()
    {
        var current = Facts();
        var unknown = new[]
        {
            current with { InstitutionalRightsActive = null },
            current with { HonorAllowsAttendance = null },
            current with { HonorAllowsSuffrage = null },
            current with { RegimenAllowsSuffrage = null },
            current with { MemberActive = null }
        };
        foreach (var facts in unknown)
        {
            var result = AssemblyVoterEligibilityPolicy.Evaluate(facts);
            Assert.Equal(AssemblyVoterEligibilityPolicy.Pending, result.Status);
            Assert.False(result.MayVote);
        }
    }

    [Fact]
    public void Not_assembly_member_and_restricted_attendance_never_votes()
    {
        var notAssemblyMember = AssemblyVoterEligibilityPolicy.Evaluate(Facts() with { IsAssemblyMember = false });
        Assert.False(notAssemblyMember.MayVote);
        Assert.False(notAssemblyMember.MayAttend);

        var noAttendance = AssemblyVoterEligibilityPolicy.Evaluate(Facts() with { RegimenAllowsAttendance = false });
        Assert.Equal(AssemblyVoterEligibilityPolicy.Ineligible, noAttendance.Status);
        Assert.False(noAttendance.MayAttend);
    }
}
