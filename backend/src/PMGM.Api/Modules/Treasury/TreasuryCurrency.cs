using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Treasury;

public static class TreasuryCurrency
{
    public const string Clp = "CLP";
    public const string Usd = "USD";

    public static string ForTerritory(string? territory) => territory == GrandTreasuryFeeSchedule.Peru ? Usd : Clp;

    public static bool IsSupported(string? currency) => currency is Clp or Usd;

    public static string? Select(string? requested, string fallback)
    {
        if (string.IsNullOrWhiteSpace(requested)) return fallback;
        var normalized = requested.Trim().ToUpperInvariant();
        return IsSupported(normalized) ? normalized : null;
    }

    public static async Task<string> ForOrganizationAsync(PmgmDbContext db, Guid organizationId, CancellationToken ct) =>
        ForTerritory(await WorkshopOriente.TerritoryAsync(db, organizationId, ct));
}
