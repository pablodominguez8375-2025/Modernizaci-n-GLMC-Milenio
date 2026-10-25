using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Único límite de confianza de C6: las claves públicas se leen del servidor,
/// nunca del payload HTTP, de la firma ni de una interfaz. Un registro ausente
/// o inválido falla cerrado y no permite certificar saldo ni votar.
/// </summary>
public sealed class AssemblyGrandTreasuryServerTrustRegistry(IConfiguration configuration)
{
    public const string ConfigurationSection = "AssemblyGrandTreasury:TrustedReviewKeys";

    public AssemblyGrandTreasuryReviewSignatureResult Verify(
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        AssemblyGrandTreasuryReviewSignature signature,
        ReadOnlySpan<byte> documentBytes,
        DateTimeOffset verifiedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(signature);

        if (!TryLoad(out var trusted))
            return new AssemblyGrandTreasuryReviewSignatureResult(
                false, false, ["SERVER_TRUST_REGISTRY_NOT_READY"]);

        return AssemblyGrandTreasuryReviewSignaturePolicy.Verify(
            proposal, review, signature, documentBytes, trusted, verifiedAtUtc);
    }

    private bool TryLoad(out IReadOnlyCollection<GrandTreasuryTrustedVerificationKey> trusted)
    {
        trusted = [];
        var rows = configuration.GetSection(ConfigurationSection).GetChildren().ToArray();
        if (rows.Length is < 1 or > 100) return false;

        var items = new List<GrandTreasuryTrustedVerificationKey>(rows.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var keyId = row["KeyId"];
            var subject = row["ReviewerSubject"];
            var pem = row["PublicKeyPem"];
            var from = row["ValidFromUtc"];
            var until = row["ValidUntilUtc"];
            var revoked = row["RevokedAtUtc"];

            if (row["Approved"] != "true" ||
                string.IsNullOrWhiteSpace(keyId) || keyId.Length > 100 ||
                keyId.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.')) ||
                !seen.Add(keyId) ||
                string.IsNullOrWhiteSpace(subject) || subject.Length > 320 ||
                string.IsNullOrWhiteSpace(pem) ||
                !pem.TrimStart().StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal) ||
                !TryIsoTime(from, out var start) ||
                !TryIsoTime(until, out var end) || start > end)
                return false;

            DateTimeOffset? revokedAt = null;
            if (revoked is not null)
            {
                if (!TryIsoTime(revoked, out var revokedDate)) return false;
                revokedAt = revokedDate;
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(pem);
                if (rsa.KeySize < 2048 || rsa.KeySize > 8192)
                    return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            items.Add(new GrandTreasuryTrustedVerificationKey(
                keyId, subject, pem, start, end, revokedAt));
        }

        trusted = items;
        return true;
    }

    // Formato ISO8601 round-trip inequívoco con offset; no se infiere zona local.
    private static bool TryIsoTime(string? value, out DateTimeOffset result) =>
        DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out result);
}
