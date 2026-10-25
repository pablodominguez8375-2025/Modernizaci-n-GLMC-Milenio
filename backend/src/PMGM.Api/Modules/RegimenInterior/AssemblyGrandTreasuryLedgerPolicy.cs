namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Contrato de saldo por taller acreditado por Gran Tesorería. NO es una lectura
/// de estado cualitativo: cada mes debe estar incluido y certificado.
/// La futura capa de infraestructura es responsable de autenticar la fuente,
/// comprobar rectificaciones, certificar la deuda de apertura anterior a la
/// cobertura y construir este paquete en servidor. No aceptar flags del cliente.
/// </summary>
public static class AssemblyGrandTreasuryLedgerPolicy
{
    public static AssemblyGrandTreasuryBalance? Project(AssemblyTreasuryCertifiedLedger ledger)
    {
        if (ledger.OrganizationId == Guid.Empty ||
            ledger.CoverageFrom == DateOnly.MinValue ||
            ledger.CoverageFrom > ledger.Cutoff ||
            ledger.CoverageFrom.Day != 1 ||
            !ledger.OpeningBalanceCertified ||
            ledger.OpeningBalanceAsOf != ledger.CoverageFrom.AddDays(-1) ||
            ledger.OpeningPendingAmount is null || ledger.OpeningPendingAmount < 0m ||
            string.IsNullOrWhiteSpace(ledger.OpeningBalanceSourceReference) ||
            !ledger.SourceAuthenticated || !ledger.CompletenessCertified ||
            string.IsNullOrWhiteSpace(ledger.CertificationReference) ||
            string.IsNullOrWhiteSpace(ledger.Currency) ||
            ledger.Periods is null || ledger.Periods.Count == 0)
            return null;

        var byPeriod = ledger.Periods.GroupBy(x => (x.PeriodYear, x.PeriodMonth)).ToArray();
        if (byPeriod.Any(x => x.Count() != 1)) return null; // Rectificaciones aún no resueltas.
        if (ledger.Periods.Any(x =>
                x.OrganizationId != ledger.OrganizationId ||
                x.Currency != ledger.Currency ||
                !x.GrandTreasuryCertified ||
                string.IsNullOrWhiteSpace(x.SourceReference) ||
                x.ExpectedAmount < 0m || x.PaidAmount < 0m ||
                x.PaidAmount > x.ExpectedAmount ||
                x.CertifiedAsOf != ledger.Cutoff))
            return null;

        // Todo mes entre el comienzo certificado y el corte debe tener
        // una sola línea. No deducir saldo cero de la ausencia de cuadros.
        var from = new DateOnly(ledger.CoverageFrom.Year, ledger.CoverageFrom.Month, 1);
        var until = new DateOnly(ledger.Cutoff.Year, ledger.Cutoff.Month, 1);
        int expectedMonths = (until.Year - from.Year) * 12 + until.Month - from.Month + 1;
        if (expectedMonths <= 0 || byPeriod.Length != expectedMonths)
            return null;

        for (var month = from; month <= until; month = month.AddMonths(1))
        {
            if (!byPeriod.Any(x => x.Key == (month.Year, month.Month)))
                return null;
        }

        // La deuda de apertura certificada por GT forma parte del saldo:
        // no se infiere deuda cero a partir de cuadros mensuales parciales.
        var amount = ledger.OpeningPendingAmount.Value +
            ledger.Periods.Sum(x => x.ExpectedAmount - x.PaidAmount);
        return new AssemblyGrandTreasuryBalance(
            ledger.OrganizationId, ledger.Cutoff,
            AssemblyVoterEligibilityPolicy.GrandTreasurySource,
            ledger.CertificationReference, ledger.Currency, amount, true);
    }
}

public sealed record AssemblyTreasuryCertifiedLedger(
    Guid OrganizationId,
    DateOnly CoverageFrom,
    DateOnly Cutoff,
    string Currency,
    string CertificationReference,
    bool SourceAuthenticated,
    bool CompletenessCertified,
    IReadOnlyList<AssemblyTreasuryCertifiedPeriod> Periods,
    DateOnly? OpeningBalanceAsOf = null,
    decimal? OpeningPendingAmount = null,
    string? OpeningBalanceSourceReference = null,
    bool OpeningBalanceCertified = false);

public sealed record AssemblyTreasuryCertifiedPeriod(
    Guid OrganizationId,
    int PeriodYear,
    int PeriodMonth,
    DateOnly CertifiedAsOf,
    string Currency,
    decimal ExpectedAmount,
    decimal PaidAmount,
    bool GrandTreasuryCertified,
    string SourceReference);
