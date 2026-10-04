using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public sealed record TariffRate(string FeeType, string Territory, decimal Amount, string Currency);
public sealed record TariffCeremonyRate(string CeremonyType, string Territory, decimal Amount, string Currency);
public sealed record TariffUnemploymentRate(string Territory, int Quarter, decimal DiscountPercent, decimal Amount, string Currency);
public sealed record TariffValues(TariffRate[] Rates, TariffCeremonyRate[] CeremonyRights, TariffUnemploymentRate[] Unemployment);
public sealed record TariffVersionDto(Guid Id, int Version, string Number, DateOnly DecreeDate,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil, string SourceReference, string Status,
    TariffRate[] Rates, TariffCeremonyRate[] CeremonyRights, TariffUnemploymentRate[] Unemployment);
public sealed record RegisterTariffRequest(int ExpectedVersion, string Number, DateOnly DecreeDate,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil, string SourceReference, string Status,
    TariffRate[] Rates, TariffCeremonyRate[] CeremonyRights, TariffUnemploymentRate[] Unemployment);
public readonly record struct TariffResolution(decimal Amount, string Currency, Guid TariffVersionId, int Version, string SourceReference);

public static class GrandTreasuryTariff
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Santiago"));
    public static DateOnly NextPeriod(DateOnly today) => new DateOnly(today.Year, today.Month, 1).AddMonths(1);
    public static TariffVersionDto ToDto(GrandTreasuryTariffVersion row)
    {
        var values = JsonSerializer.Deserialize<TariffValues>(row.Payload, Json)
            ?? throw new InvalidOperationException("Tarifario persistido inválido.");
        return new(row.Id, row.Version, row.Number, row.DecreeDate, row.EffectiveFrom, row.EffectiveUntil,
            row.SourceReference, row.Status, values.Rates, values.CeremonyRights, values.Unemployment);
    }
    public static Task<List<GrandTreasuryTariffVersion>> LoadRowsAsync(PmgmDbContext db, CancellationToken ct)
        => db.GrandTreasuryTariffVersions.AsNoTracking().OrderBy(x => x.Version).ToListAsync(ct);
    public static async Task<List<TariffVersionDto>> LoadAsync(PmgmDbContext db, CancellationToken ct)
        => (await LoadRowsAsync(db, ct)).Select(ToDto).ToList();
    public static TariffVersionDto? At(IEnumerable<TariffVersionDto> catalog, DateOnly date)
    {
        var latest = catalog.Where(x => x.Status == "published" && x.EffectiveFrom <= date)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).FirstOrDefault();
        return latest is not null && (latest.EffectiveUntil is null || latest.EffectiveUntil >= date) ? latest : null;
    }
    public static TariffResolution? Resolve(IEnumerable<TariffVersionDto> catalog, string feeType, string territory, DateOnly date)
    {
        var version = At(catalog, date);
        var rows = version?.Rates.Where(x => x.FeeType == feeType && x.Territory == territory).ToArray();
        return version is not null && rows is { Length: 1 }
            ? new(rows[0].Amount, rows[0].Currency, version.Id, version.Version, version.SourceReference) : null;
    }
    public static TariffResolution? ResolveCeremonyRight(IEnumerable<TariffVersionDto> catalog, string ceremonyType, string territory, DateOnly date)
    {
        var version = At(catalog, date);
        var rows = version?.CeremonyRights.Where(x => x.CeremonyType == ceremonyType && x.Territory == territory).ToArray();
        return version is not null && rows is { Length: 1 }
            ? new(rows[0].Amount, rows[0].Currency, version.Id, version.Version, version.SourceReference) : null;
    }
    public static async Task<TariffResolution?> ResolveAsync(PmgmDbContext db, string feeType, string territory, DateOnly date, CancellationToken ct)
        => Resolve(await LoadAsync(db, ct), feeType, territory, date);
    public static async Task<TariffResolution?> ResolveCeremonyAsync(PmgmDbContext db, Guid requestId, Guid organizationId,
        string ceremonyType, DateOnly date, CancellationToken ct)
    {
        // A recorded payment locks the original monetary snapshot, even after a Ficha/decree change.
        var firstPaid = await db.CeremonyRightPayments.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId).OrderBy(x => x.RecordedAtUtc)
            .Select(x => new { x.PaymentDate, x.TariffVersionId, x.RightAmount, x.Currency }).FirstOrDefaultAsync(ct);
        var catalog = await LoadAsync(db, ct);
        if (firstPaid?.TariffVersionId is Guid id && firstPaid.RightAmount is decimal amount)
        {
            var locked = catalog.Single(x => x.Id == id);
            return new(amount, firstPaid.Currency, id, locked.Version, locked.SourceReference);
        }
        var territory = await WorkshopOriente.TerritoryAsync(db, organizationId, ct);
        return territory is null ? null : ResolveCeremonyRight(catalog, ceremonyType, territory, firstPaid?.PaymentDate ?? date);
    }

    public static string? Validate(RegisterTariffRequest value, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(value.Number) || value.Number.Length > 80 ||
            string.IsNullOrWhiteSpace(value.SourceReference) || value.SourceReference.Length > 1200)
            return "Número de decreto y PDF o referencia de respaldo son obligatorios.";
        if (value.Status is not ("draft" or "published")) return "El estado debe ser borrador o publicado.";
        if (value.EffectiveFrom < NextPeriod(today) || value.DecreeDate > value.EffectiveFrom ||
            value.DecreeDate < new DateOnly(1900, 1, 1) || value.EffectiveUntil < value.EffectiveFrom)
            return "La nueva vigencia debe comenzar en un período futuro; revise fecha de decreto y vigencia final.";
        if (value.Rates is null || value.CeremonyRights is null || value.Unemployment is null ||
            value.Rates.Length > 30 || value.CeremonyRights.Length > 30 || value.Unemployment.Length > 12)
            return "Revise las tablas del decreto.";
        if (value.Rates.GroupBy(x => (x.Territory, x.FeeType)).Any(x => x.Count() != 1) ||
            value.CeremonyRights.GroupBy(x => (x.Territory, x.CeremonyType)).Any(x => x.Count() != 1) ||
            value.Unemployment.GroupBy(x => (x.Territory, x.Quarter)).Any(x => x.Count() != 1))
            return "No puede repetirse una zona y categoría, ceremonia o trimestre.";
        static bool Amount(decimal amount, string currency) => amount >= 0 && amount <= 1000000000m &&
            TreasuryCurrency.IsSupported(currency) && decimal.Round(amount, currency == "CLP" ? 0 : 2) == amount;
        if (value.Rates.Any(x => !GrandTreasuryFeeSchedule.IsValidTerritory(x.Territory) ||
            !TreasuryCodes.LodgeFeeType.IsValid(x.FeeType) || !Amount(x.Amount, x.Currency) ||
            x.Currency != TreasuryCurrency.ForTerritory(x.Territory) ||
            (x.FeeType == TreasuryCodes.LodgeFeeType.PastActive && x.Amount != 0)))
            return "Cuotas: revise zona, categoría, monto y moneda. Past Activo conserva su exención ordinaria.";
        if (value.CeremonyRights.Any(x => !GrandTreasuryFeeSchedule.IsValidTerritory(x.Territory) ||
            !GrandTreasuryFeeSchedule.IsChargedCeremony(x.CeremonyType) || !Amount(x.Amount, x.Currency)))
            return "Revise los derechos por zona, ceremonia y moneda.";
        if (value.Unemployment.Any(x => !GrandTreasuryFeeSchedule.IsValidTerritory(x.Territory) ||
            x.Quarter is < 1 or > 4 || x.DiscountPercent is < 0 or > 100 ||
            !Amount(x.Amount, x.Currency) || x.Currency != TreasuryCurrency.ForTerritory(x.Territory)))
            return "Revise la tabla de cesantía: porcentaje de rebaja, trimestre, monto y moneda.";
        return null;
    }
}
