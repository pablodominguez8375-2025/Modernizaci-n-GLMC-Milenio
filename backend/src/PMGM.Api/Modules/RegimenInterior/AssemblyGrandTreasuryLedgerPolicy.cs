namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Contrato de saldo por taller acreditado por Gran Tesorería. NO es una lectura
/// de estado cualitativo: cada mes debe estar incluido y certificado.
/// La futura capa de infraestructura es responsable de autenticar la fuente,
/// comprobar rectificaciones y construir este paquete en servidor.
/// </summary>
public static class AssemblyGrandTreasuryLedgerPolicy
{
    public static AssemblyGrandTreasuryBalance? Project(AssemblyTreasuryCertifiedLedger ledger)
    {
        if (ledger.OrganizationId == Guid.Empty || ledger.CoverageFrom > ledger.Cutoff ||
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

        var amount = ledger.Periods.Sum(x => x.ExpectedAmount - x.PaidAmount);
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
    IReadOnlyList<AssemblyTreasuryCertifiedPeriod> Periods);

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
