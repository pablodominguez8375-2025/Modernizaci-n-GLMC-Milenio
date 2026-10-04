using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Treasury;

public static class WorkshopOriente
{
    public static bool IsValid(string? value) => value is "santiago" or "other_chile" or "peru";
    public static string? FromLocation(string? city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city)) return null;
        return country?.Trim().ToLowerInvariant() switch
        {
            "chile" => city.Trim().Equals("Santiago", StringComparison.OrdinalIgnoreCase) ? "santiago" : "other_chile",
            "perú" or "peru" => "peru",
            _ => null
        };
    }
    public static string? Territory(Organization organization)
        => FromLocation(organization.City, organization.Country) switch
        {
            "santiago" => GrandTreasuryFeeSchedule.Santiago,
            "other_chile" => GrandTreasuryFeeSchedule.OtherOriente,
            "peru" => GrandTreasuryFeeSchedule.Peru,
            _ => null
        };
    public static async Task<string?> TerritoryAsync(PmgmDbContext db, Guid organizationId, CancellationToken ct)
    {
        var organization = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == organizationId, ct);
        return organization is null ? null : Territory(organization);
    }
}
