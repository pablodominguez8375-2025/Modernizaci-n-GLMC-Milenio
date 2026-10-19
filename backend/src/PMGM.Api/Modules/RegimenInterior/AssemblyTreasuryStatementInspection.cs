using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Lectura diagnóstica interna de cuadros GT: constata integridad de las
/// filas observadas, nunca certifica que el histórico total de obligaciones
/// esté completo, ni autoriza sufragio. No hay endpoint HTTP para este servicio.
/// </summary>
public sealed class AssemblyTreasuryStatementInspection(PmgmDbContext db)
{
    public async Task<AssemblyTreasuryInspectionResult> InspectAsync(
        Guid organizationId, DateOnly from, DateOnly cutoff, CancellationToken ct)
    {
        if (organizationId == Guid.Empty || from > cutoff)
            return Inspect(organizationId, from, cutoff, []);

        var rows = await db.TreasuryMonthlyStatements.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId &&
                (x.PeriodYear > from.Year ||
                    (x.PeriodYear == from.Year && x.PeriodMonth >= from.Month)) &&
                (x.PeriodYear < cutoff.Year ||
                    (x.PeriodYear == cutoff.Year && x.PeriodMonth <= cutoff.Month)))
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .AsSplitQuery()
            .ToListAsync(ct);
        return Inspect(organizationId, from, cutoff, rows);
    }

    public static AssemblyTreasuryInspectionResult Inspect(
        Guid organizationId, DateOnly from, DateOnly cutoff,
        IEnumerable<TreasuryMonthlyStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);
        var issues = new HashSet<string>(StringComparer.Ordinal);
        var items = statements.ToArray();
        if (organizationId == Guid.Empty || from > cutoff)
            issues.Add("INVALID_SCOPE_OR_CUTOFF");

        // Los cuadros mensuales se inspeccionan desde el comienzo del mes de
        // cobertura. La declaración de cobertura histórica no se deriva aquí.
        var startMonth = new DateOnly(from.Year, from.Month, 1);
        var cutoffMonth = new DateOnly(cutoff.Year, cutoff.Month, 1);
        var expected = from <= cutoff
            ? (cutoffMonth.Year - startMonth.Year) * 12 + cutoffMonth.Month - startMonth.Month + 1
            : 0;

        var groups = items.GroupBy(x => (x.PeriodYear, x.PeriodMonth)).ToArray();
        if (groups.Any(x => x.Count() != 1 ||
                            x.Any(y => y.RectifiesStatementId is not null ||
                                y.Status == TreasuryCodes.StatementStatus.Rectified)))
            issues.Add("RECTIFICATION_OR_DUPLICATE_UNRESOLVED");

        if (groups.Length != expected ||
            (expected > 0 && groups.Any(x =>
                x.Key.PeriodYear < startMonth.Year ||
                (x.Key.PeriodYear == startMonth.Year && x.Key.PeriodMonth < startMonth.Month) ||
                x.Key.PeriodYear > cutoffMonth.Year ||
                (x.Key.PeriodYear == cutoffMonth.Year && x.Key.PeriodMonth > cutoffMonth.Month))))
            issues.Add("MISSING_OR_OUT_OF_SCOPE_PERIOD");

        for (var month = startMonth; month <= cutoffMonth && expected > 0; month = month.AddMonths(1))
            if (!groups.Any(x => x.Key == (month.Year, month.Month)))
                issues.Add("MISSING_OR_OUT_OF_SCOPE_PERIOD");

        if (items.Any(x => x.OrganizationId != organizationId))
            issues.Add("FOREIGN_WORKSHOP");
        if (items.Any(x => x.PeriodMonth is < 1 or > 12 ||
                x.CutoffDate.Year != x.PeriodYear ||
                x.CutoffDate.Month != x.PeriodMonth ||
                x.CutoffDate > cutoff))
            issues.Add("INVALID_STATEMENT_CUTOFF");
        if (items.Any(x => x.Status is not
                (TreasuryCodes.StatementStatus.Reconciled or TreasuryCodes.StatementStatus.Closed)))
            issues.Add("NOT_RECONCILED");
        if (items.Any(x => string.IsNullOrWhiteSpace(x.SourceReference)))
            issues.Add("MISSING_STATEMENT_REFERENCE");

        var currencies = items.Select(x => x.Currency.Trim().ToUpperInvariant())
            .Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        if (currencies.Length != 1 || items.Any(x => string.IsNullOrWhiteSpace(x.Currency)))
            issues.Add("INCOMPATIBLE_CURRENCY");

        foreach (var statement in items)
        {
            if (statement.ReconciledAtUtc is null ||
                DateOnly.FromDateTime(statement.ReconciledAtUtc.Value.UtcDateTime) > cutoff ||
                statement.BankReceiptConfirmedAtUtc is null ||
                DateOnly.FromDateTime(statement.BankReceiptConfirmedAtUtc.Value.UtcDateTime) > cutoff ||
                string.IsNullOrWhiteSpace(statement.BankReceiptConfirmedBySubject))
                issues.Add("UNCONFIRMED_OR_LATE_BANK_RECONCILIATION");

            if (statement.Lines.Count == 0 ||
                statement.Lines.Any(x =>
                    x.IdentityMatchStatus != TreasuryCodes.IdentityMatchStatus.Matched ||
                    x.PayableAmount < 0m))
                issues.Add("INVALID_OBLIGATION_OR_IDENTITY");

            if (statement.Payments.Any(x => x.Amount <= 0m ||
                x.PaymentDate > cutoff ||
                DateOnly.FromDateTime(x.RecordedAtUtc.UtcDateTime) > cutoff ||
                !TreasuryCodes.PaymentMethod.IsValid(x.PaymentMethod)))
                issues.Add("INVALID_OR_LATE_PAYMENT");

            var totals = TreasuryStatementTotals.Calculate(statement.Lines, statement.Payments);
            if (totals.DifferenceAmount != 0m)
                issues.Add("UNBALANCED_STATEMENT");
        }

        // Distinción fundamental: suma/diferencia de los CUADROS observados
        // no equivale al saldo institucional histórico de Gran Tesorería.
        var observedDifference = issues.Count == 0
            ? items.Sum(x => TreasuryStatementTotals.Calculate(x.Lines, x.Payments).DifferenceAmount)
            : (decimal?)null;

        issues.Add("HISTORICAL_CERTIFICATION_REQUIRED");

        return new AssemblyTreasuryInspectionResult(
            organizationId, from, cutoff, expected, groups.Length,
            issues.Count == 1, observedDifference,
            issues.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}

public sealed record AssemblyTreasuryInspectionResult(
    Guid OrganizationId,
    DateOnly CoverageFrom,
    DateOnly Cutoff,
    int ExpectedPeriods,
    int ObservedPeriods,
    bool ObservedRowsConsistent,
    decimal? ObservedStatementDifference,
    IReadOnlyList<string> BlockingReasons)
{
    // No certificado para sufragio ni devolución de saldos reales.
    public bool MayAuthorizeSuffrage => false;
}
