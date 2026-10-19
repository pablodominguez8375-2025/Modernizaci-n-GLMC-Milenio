using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyGrandTreasuryLedgerPolicyTests
{
    private static readonly Guid Workshop = Guid.NewGuid();
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static AssemblyTreasuryCertifiedPeriod Month(int month, decimal due = 1000m,
        decimal paid = 1000m) =>
        new(Workshop, 2026, month, Cutoff, "CLP", due, paid, true,
            $"GT-2026-{month:00}");

    private static AssemblyTreasuryCertifiedLedger Ledger(params AssemblyTreasuryCertifiedPeriod[] months) =>
        new(Workshop, new DateOnly(2026, 8, 1), Cutoff, "CLP",
            "GT-CERTIFICACION-CORTE-19-10", true, true, months,
            OpeningBalanceAsOf: new DateOnly(2026, 7, 31),
            OpeningPendingAmount: 0m,
            OpeningBalanceSourceReference: "GT-SALDO-APERTURA-31-07",
            OpeningBalanceCertified: true);

    [Fact]
    public void Every_completed_certified_period_with_zero_balance_allows_zero()
    {
        var result = AssemblyGrandTreasuryLedgerPolicy.Project(Ledger(Month(8), Month(9), Month(10)));
        Assert.NotNull(result);
        Assert.Equal(0m, result.AmountPending);
        Assert.Equal(AssemblyVoterEligibilityPolicy.GrandTreasurySource, result.SourceSystem);
        Assert.True(AssemblyVoterEligibilityPolicy.Evaluate(new(
            Workshop, Cutoff, true, true, true, true, true, true, true, result, null)).MayVote);
    }

    [Fact]
    public void Certified_old_debt_is_added_to_monthly_outstanding_amount()
    {
        var ledger = Ledger(Month(8), Month(9), Month(10)) with
        { OpeningPendingAmount = 1500m };
        var result = AssemblyGrandTreasuryLedgerPolicy.Project(ledger);
        Assert.NotNull(result);
        Assert.Equal(1500m, result.AmountPending);
        var decision = AssemblyVoterEligibilityPolicy.Evaluate(
            new(Workshop, Cutoff, true, true, true, true, true, true, true, result, null));
        Assert.True(decision.MayAttend);
        Assert.False(decision.MayVote);
        Assert.Equal("MOROSIDAD_GRAN_TESORERIA", decision.ReasonCode);
    }

    [Fact]
    public void Missing_opening_debt_evidence_must_not_be_treated_as_zero()
    {
        var known = Ledger(Month(8), Month(9), Month(10));
        var cases = new[]
        {
            known with { OpeningPendingAmount = null },
            known with { OpeningPendingAmount = -1m },
            known with { OpeningBalanceCertified = false },
            known with { OpeningBalanceSourceReference = "" },
            known with { OpeningBalanceAsOf = null },
            known with { OpeningBalanceAsOf = new DateOnly(2026, 8, 1) },
            known with { CoverageFrom = new DateOnly(2026, 8, 15) },
            known with { CoverageFrom = DateOnly.MinValue },
            known with { OpeningBalanceAsOf = new DateOnly(2026, 8, 1),
                         OpeningPendingAmount = 0m, OpeningBalanceCertified = false }
        };
        foreach (var ledger in cases)
            Assert.Null(AssemblyGrandTreasuryLedgerPolicy.Project(ledger));
    }

    [Fact]
    public void Legacy_eight_argument_ledger_fails_closed_without_opening_certificate()
    {
        var oldShape = new AssemblyTreasuryCertifiedLedger(
            Workshop, new DateOnly(2026, 8, 1), Cutoff, "CLP",
            "GT-CERTIFICACION-CORTE-19-10", true, true,
            [Month(8), Month(9), Month(10)]);
        Assert.Null(AssemblyGrandTreasuryLedgerPolicy.Project(oldShape));
    }

    [Fact]
    public void Any_positive_unpaid_amount_blocks_suffrage()
    {
        var result = AssemblyGrandTreasuryLedgerPolicy.Project(
            Ledger(Month(8), Month(9, 1000m, 999.99m), Month(10)));
        Assert.NotNull(result);
        Assert.Equal(0.01m, result.AmountPending);
        var decision = AssemblyVoterEligibilityPolicy.Evaluate(
            new(Workshop, Cutoff, true, true, true, true, true, true, true, result, null));
        Assert.True(decision.MayAttend);
        Assert.False(decision.MayVote);
        Assert.Equal("MOROSIDAD_GRAN_TESORERIA", decision.ReasonCode);
    }

    [Fact]
    public void Missing_month_duplicate_rectification_and_old_cutoff_are_not_zero()
    {
        var cases = new[]
        {
            Ledger(Month(8), Month(10)),
            Ledger(Month(8), Month(9), Month(9), Month(10)),
            Ledger(Month(8), Month(9), Month(10)) with { Cutoff = Cutoff.AddDays(1) },
            Ledger(Month(8), Month(9), Month(10)) with { CoverageFrom = Cutoff.AddMonths(1) }
        };
        foreach (var sample in cases)
            Assert.Null(AssemblyGrandTreasuryLedgerPolicy.Project(sample));
    }

    [Fact]
    public void Wrong_source_unverified_partial_foreign_currency_or_overpayment_is_pending()
    {
        var baseLine = Ledger(Month(8), Month(9), Month(10));
        var cases = new[]
        {
            baseLine with { SourceAuthenticated = false },
            baseLine with { CompletenessCertified = false },
            baseLine with { CertificationReference = "" },
            baseLine with { Periods = [Month(8), Month(9), Month(10) with { OrganizationId = Guid.NewGuid() }] },
            baseLine with { Periods = [Month(8), Month(9), Month(10) with { Currency = "USD" }] },
            baseLine with { Periods = [Month(8), Month(9), Month(10) with { GrandTreasuryCertified = false }] },
            baseLine with { Periods = [Month(8), Month(9), Month(10) with { SourceReference = "" }] },
            baseLine with { Periods = [Month(8), Month(9), Month(10) with { PaidAmount = 1001m }] },
            baseLine with { Periods = [Month(8), Month(9), Month(10) with { ExpectedAmount = -1m }] }
        };
        foreach (var sample in cases)
            Assert.Null(AssemblyGrandTreasuryLedgerPolicy.Project(sample));
    }

    [Fact]
    public void No_automatic_inference_from_treasury_statement_status_or_partial_history()
    {
        var sample = Ledger(Month(10));
        Assert.Null(AssemblyGrandTreasuryLedgerPolicy.Project(sample));

        var missingCertification = Ledger(Month(8), Month(9), Month(10)) with
        { SourceAuthenticated = true, CompletenessCertified = false };
        Assert.Null(AssemblyGrandTreasuryLedgerPolicy.Project(missingCertification));
    }
}
