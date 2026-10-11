using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PMGM.Api.Modules.GrandSecretariat;

/// <summary>Numeración anual de planchas, separada de los códigos históricos aleatorios.</summary>
public static class CeremonyAuthorizationPlanchaPolicy
{
    public static string Prefix(int chileYear) => $"PLA-AUT-CER-{chileYear:D4}-";

    public static string NextCode(int chileYear, IEnumerable<string> existingCodes)
    {
        var prefix = Prefix(chileYear);
        var usedNumbers = existingCodes
            .Where(code => code.StartsWith(prefix, StringComparison.Ordinal))
            .Select(code => code[prefix.Length..])
            .Where(suffix => suffix.Length == 6 &&
                             int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .Select(suffix => int.Parse(suffix, CultureInfo.InvariantCulture))
            .ToArray();
        var next = usedNumbers.Length == 0 ? 1 : checked(usedNumbers.Max() + 1);
        if (next > 999999) throw new InvalidOperationException("Correlativo anual de planchas agotado.");
        return prefix + next.ToString("D6", CultureInfo.InvariantCulture);
    }

    public static string SnapshotDigest(string? frozenAuditMetadata)
        => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(frozenAuditMetadata ?? ""))).ToLowerInvariant();

    public static string Render(string documentCode, Guid requestId, string workshopName,
        string ceremonyType, string dateText, string spaceText, Guid authorizationAuditId,
        string authorizationAuditDigest)
        => $"GRAN LOGIA MIXTA DE CHILE — GRAN SECRETARÍA\n" +
           $"PLANCHA DE AUTORIZACIÓN Nº {documentCode}\n" +
           $"Expediente: {requestId:D}\n" +
           $"Se autoriza la ceremonia '{ceremonyType}' solicitada por {workshopName}, " +
           $"para {dateText}, {spaceText}.\n" +
           $"Fundamento: autorización institucional previamente aprobada; " +
           $"registro de auditoría {authorizationAuditId:D}; " +
           $"SHA-256 de su snapshot {authorizationAuditDigest}.\n" +
           "La emisión de esta plancha no sustituye la firma institucional y no constituye Decreto.";
}
