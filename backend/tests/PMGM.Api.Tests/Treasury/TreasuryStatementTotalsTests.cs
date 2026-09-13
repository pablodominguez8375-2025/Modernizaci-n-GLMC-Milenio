using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Treasury;

public sealed class TreasuryStatementTotalsTests
{
    [Fact]
    public void Calculate_supports_authorized_adjustments_and_partial_payments()
    {
        var lines = new[]
        {
            Line(21_000m, 0m),
            Line(21_000m, -13_000m),
            Line(21_000m, -21_000m)
        };
        var payments = new[]
        {
            Payment(TreasuryCodes.PaymentMethod.Transfer, 20_000m),
            Payment(TreasuryCodes.PaymentMethod.Deposit, 9_000m)
        };

        var result = TreasuryStatementTotals.Calculate(lines, payments);

        Assert.Equal(29_000m, result.ExpectedAmount);
        Assert.Equal(20_000m, result.TransferAmount);
        Assert.Equal(9_000m, result.DepositAmount);
        Assert.Equal(29_000m, result.PaidAmount);
        Assert.Equal(0m, result.DifferenceAmount);
    }

    [Fact]
    public void Calculate_exposes_underpayment_as_positive_difference()
    {
        var result = TreasuryStatementTotals.Calculate(
            new[] { Line(21_000m, 0m) },
            new[] { Payment(TreasuryCodes.PaymentMethod.Transfer, 20_000m) });

        Assert.Equal(1_000m, result.DifferenceAmount);
    }

    private static TreasuryMonthlyStatementLine Line(decimal baseAmount, decimal adjustmentAmount) => new()
    {
        DegreeCodeAtCutoff = "master",
        BaseAmount = baseAmount,
        AdjustmentAmount = adjustmentAmount,
        IdentityMatchStatus = TreasuryCodes.IdentityMatchStatus.Matched
    };

    private static TreasuryPayment Payment(string method, decimal amount) => new()
    {
        PaymentMethod = method,
        PaymentDate = new DateOnly(2026, 7, 10),
        Amount = amount,
        PayerDisplayName = "Tesorero de prueba"
    };
}
