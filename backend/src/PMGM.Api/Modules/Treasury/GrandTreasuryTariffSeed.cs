using System.Text.Json;
namespace PMGM.Api.Modules.Treasury;

// Initial migration data only. Operational resolution never falls back to this resource.
public static class GrandTreasuryTariffSeed
{
    public static TariffVersionDto Load()
    {
        using var stream = typeof(GrandTreasuryTariffSeed).Assembly.GetManifestResourceStream("PMGM.Decree1759")
            ?? throw new InvalidOperationException("Falta la fuente de migración del Decreto 1.759.");
        return JsonSerializer.Deserialize<TariffVersionDto>(stream, GrandTreasuryTariff.Json)
            ?? throw new InvalidOperationException("Fuente inicial inválida.");
    }
}
