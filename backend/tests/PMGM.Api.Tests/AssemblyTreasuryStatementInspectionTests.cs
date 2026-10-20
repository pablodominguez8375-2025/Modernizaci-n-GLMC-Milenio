using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyTreasuryStatementInspectionTests
{
    private static readonly Guid Lodge = Guid.Parse("571ca729-1e3b-4ac8-b890-21d1a78ae8dd");
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static TreasuryMonthlyStatement Statement(int month, string currency = "CLP")
    {
        var statement = new TreasuryMonthlyStatement
        {
            OrganizationId = Lodge,
            PeriodYear = 2026,
            PeriodMonth = month,
            CutoffDate = new DateOnly(2026, month, Math.Min(19, DateTime.DaysInMonth(2026, month))),
            Currency = currency,
            Status = TreasuryCodes.StatementStatus.Reconciled,
            SourceReference = $"CUADRO-GT-2026-{month:00}",
            ReconciledAtUtc = new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero),
            BankReceiptConfirmedAtUtc = new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero),
            BankReceiptConfirmedBySubject = "qa-gran-tesorero"
        };
        statement.Lines.Add(new TreasuryMonthlyStatementLine
        {
            StatementId = statement.Id,
            DegreeCodeAtCutoff = TreasuryCodes.Degree.Master,
            IdentityMatchStatus = TreasuryCodes.IdentityMatchStatus.Matched,
            BaseAmount = 1000m
        });
        statement.Payments.Add(new TreasuryPayment
        {
            StatementId = statement.Id,
            PaymentMethod = TreasuryCodes.PaymentMethod.Transfer,
            PaymentDate = new DateOnly(2026, month, 17),
            // Fecha fija dentro del corte: el test debe ser estable después
            // de la medianoche UTC, sin flexibilizar la regla productiva.
            RecordedAtUtc = new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero),
            PayerDisplayName = "Taller ficticio",
            Amount = 1000m
        });
        return statement;
    }

    private static AssemblyTreasuryInspectionResult Inspect(params TreasuryMonthlyStatement[] rows) =>
        AssemblyTreasuryStatementInspection.Inspect(
            Lodge, new DateOnly(2026, 8, 1), Cutoff, rows);

    [Fact]
    public void Complete_reconciled_statements_are_still_not_a_certified_historical_balance()
    {
        var r = Inspect(Statement(8), Statement(9), Statement(10));
        Assert.True(r.ObservedRowsConsistent);
        Assert.Equal(3, r.ExpectedPeriods);
        Assert.Equal(3, r.ObservedPeriods);
        Assert.Equal(0m, r.ObservedStatementDifference);
        Assert.False(r.MayAuthorizeSuffrage);
        Assert.Equal(new[] { "HISTORICAL_CERTIFICATION_REQUIRED" }, r.BlockingReasons);
    }

    [Fact]
    public void Missing_months_duplicates_or_rectifications_block_promotion()
    {
        var absent = Inspect(Statement(8), Statement(10));
        Assert.Contains("MISSING_OR_OUT_OF_SCOPE_PERIOD", absent.BlockingReasons);
        Assert.False(absent.ObservedRowsConsistent);

        var duplicate = Inspect(Statement(8), Statement(9), Statement(9), Statement(10));
        Assert.Contains("RECTIFICATION_OR_DUPLICATE_UNRESOLVED", duplicate.BlockingReasons);
        Assert.Null(duplicate.ObservedStatementDifference);

        var rectified = Statement(9);
        rectified.RectifiesStatementId = Guid.NewGuid();
        Assert.Contains("RECTIFICATION_OR_DUPLICATE_UNRESOLVED",
            Inspect(Statement(8), rectified, Statement(10)).BlockingReasons);
    }

    [Fact]
    public void Multi_currency_or_wrong_workshop_never_becomes_balance_for_voting()
    {
        var mixed = Inspect(Statement(8), Statement(9, "USD"), Statement(10));
        Assert.Contains("INCOMPATIBLE_CURRENCY", mixed.BlockingReasons);
        Assert.False(mixed.MayAuthorizeSuffrage);

        var nullCurrency = Statement(9);
        nullCurrency.Currency = null!;
        var rejectedNull = Inspect(Statement(8), nullCurrency, Statement(10));
        Assert.Contains("INCOMPATIBLE_CURRENCY", rejectedNull.BlockingReasons);
        Assert.False(rejectedNull.MayAuthorizeSuffrage);

        var foreign = Statement(9);
        foreign.OrganizationId = Guid.NewGuid();
        var result = Inspect(Statement(8), foreign, Statement(10));
        Assert.Contains("FOREIGN_WORKSHOP", result.BlockingReasons);
        Assert.Null(result.ObservedStatementDifference);
    }

    [Fact]
    public void Draft_or_late_bank_confirmation_cannot_be_certified()
    {
        var draft = Statement(8);
        draft.Status = TreasuryCodes.StatementStatus.Draft;
        Assert.Contains("NOT_RECONCILED",
            Inspect(draft, Statement(9), Statement(10)).BlockingReasons);

        var late = Statement(9);
        late.BankReceiptConfirmedAtUtc = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        Assert.Contains("UNCONFIRMED_OR_LATE_BANK_RECONCILIATION",
            Inspect(Statement(8), late, Statement(10)).BlockingReasons);
    }

    [Fact]
    public void Difference_unmatched_identity_unreferenced_source_or_late_payment_blocks()
    {
        var partial = Statement(8);
        partial.Payments.Single().Amount = 900m;
        Assert.Contains("UNBALANCED_STATEMENT",
            Inspect(partial, Statement(9), Statement(10)).BlockingReasons);

        var unmatched = Statement(9);
        unmatched.Lines.Single().IdentityMatchStatus = TreasuryCodes.IdentityMatchStatus.Pending;
        Assert.Contains("INVALID_OBLIGATION_OR_IDENTITY",
            Inspect(Statement(8), unmatched, Statement(10)).BlockingReasons);

        var unreferenced = Statement(9);
        unreferenced.SourceReference = "";
        Assert.Contains("MISSING_STATEMENT_REFERENCE",
            Inspect(Statement(8), unreferenced, Statement(10)).BlockingReasons);

        var late = Statement(10);
        late.Payments.Single().PaymentDate = Cutoff.AddDays(1);
        Assert.Contains("INVALID_OR_LATE_PAYMENT",
            Inspect(Statement(8), Statement(9), late).BlockingReasons);
    }

    [Fact]
    public void Invalid_scope_and_missing_statements_are_fail_closed()
    {
        var missing = Inspect();
        Assert.False(missing.MayAuthorizeSuffrage);
        Assert.Contains("MISSING_OR_OUT_OF_SCOPE_PERIOD", missing.BlockingReasons);

        var invalid = AssemblyTreasuryStatementInspection.Inspect(Guid.Empty,
            Cutoff.AddDays(1), Cutoff, []);
        Assert.Contains("INVALID_SCOPE_OR_CUTOFF", invalid.BlockingReasons);
        Assert.False(invalid.MayAuthorizeSuffrage);
    }
}
