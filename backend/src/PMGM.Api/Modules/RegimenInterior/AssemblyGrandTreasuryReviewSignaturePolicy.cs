using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Verificador offline RSA-PSS/SHA-256 de revisión documental de GT.
/// La llave pública debe venir exclusivamente de un registro de confianza
/// del servidor. Una firma válida NO certifica deuda ni habilita voto.
/// </summary>
public static class AssemblyGrandTreasuryReviewSignaturePolicy
{
    public const string FormatVersion = "PMGM-ASSEMBLY-GT-REVIEW-SIG-V1";

    public static AssemblyGrandTreasuryReviewSignatureResult Verify(
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        AssemblyGrandTreasuryReviewSignature signature,
        ReadOnlySpan<byte> documentBytes,
        IReadOnlyCollection<GrandTreasuryTrustedVerificationKey> trustedServerKeys,
        DateTimeOffset verifiedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(trustedServerKeys);

        var problems = new HashSet<string>(StringComparer.Ordinal);
        var binding = AssemblyGrandTreasuryDocumentBindingPolicy.Check(
            signature.AssemblyId, signature.OrganizationId, proposal, review, documentBytes);
        if (!binding.ConsistentForIndependentVerification)
            problems.Add("UNVERIFIED_DOCUMENT_OR_REVIEW");

        if (signature.ProposalEventId != proposal.Id ||
            signature.ReviewEventId != review.Id ||
            string.IsNullOrWhiteSpace(signature.SignerSubject) ||
            signature.SignerSubject != review.ActorSubject ||
            string.IsNullOrWhiteSpace(signature.KeyId) ||
            signature.SignedAtUtc < review.OccurredAtUtc ||
            signature.SignedAtUtc > verifiedAtUtc)
            problems.Add("INVALID_SIGNING_SCOPE_OR_TIME");

        var matchingKeys = trustedServerKeys.Where(k =>
            string.Equals(k.KeyId, signature.KeyId, StringComparison.Ordinal)).ToArray();
        GrandTreasuryTrustedVerificationKey? key =
            matchingKeys.Length == 1 ? matchingKeys[0] : null;
        if (key is null || string.IsNullOrWhiteSpace(key.ReviewerSubject) ||
            key.ReviewerSubject != signature.SignerSubject ||
            key.ValidFromUtc > signature.SignedAtUtc ||
            key.ValidUntilUtc < signature.SignedAtUtc ||
            key.ValidFromUtc > key.ValidUntilUtc ||
            (key.RevokedAtUtc is not null && key.RevokedAtUtc <= verifiedAtUtc) ||
            string.IsNullOrWhiteSpace(key.PublicKeyPem))
            problems.Add("SIGNING_KEY_NOT_TRUSTED");

        if (problems.Count == 0)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(key!.PublicKeyPem);
                if (rsa.KeySize < 2048 ||
                    !rsa.VerifyData(
                        BuildCanonicalPayload(proposal, review, signature),
                        Convert.FromBase64String(signature.SignatureBase64),
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pss))
                    problems.Add("INVALID_SIGNATURE");
            }
            catch (CryptographicException)
            {
                problems.Add("INVALID_SIGNATURE");
            }
            catch (ArgumentException)
            {
                problems.Add("INVALID_SIGNATURE");
            }
            catch (FormatException)
            {
                problems.Add("INVALID_SIGNATURE");
            }
        }

        return new AssemblyGrandTreasuryReviewSignatureResult(
            problems.Count == 0,
            false,
            problems.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Contrato binario v1. BinaryWriter.Write(string) precede cada campo
    /// UTF-8 con su longitud codificada; impide colisiones por concatenación.
    /// Las fechas se normalizan a UTC ISO-8601 y los importes a dos decimales.
    /// </summary>
    public static byte[] BuildCanonicalPayload(
        AssemblyTreasuryCertificationEventRecord proposal,
        AssemblyTreasuryCertificationEventRecord review,
        AssemblyGrandTreasuryReviewSignature signature)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(signature);

        var fields = new[]
        {
            FormatVersion,
            signature.AssemblyId.ToString("D"),
            signature.OrganizationId.ToString("D"),
            signature.ProposalEventId.ToString("D"),
            signature.ReviewEventId.ToString("D"),
            proposal.Action,
            proposal.ActorSubject,
            proposal.CoverageFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            proposal.CutoffDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
            proposal.Currency ?? "",
            proposal.OpeningPendingAmount?.ToString("0.00", CultureInfo.InvariantCulture) ?? "",
            proposal.SourceEvidenceReference ?? "",
            proposal.EvidenceDigestSha256 ?? "",
            review.Action,
            review.ActorSubject,
            review.ProposalEventId?.ToString("D") ?? "",
            review.ReviewEvidenceReference ?? "",
            review.ReviewNote ?? "",
            review.OccurredAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            signature.KeyId,
            signature.SignerSubject,
            signature.SignedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        };
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, new UTF8Encoding(false), leaveOpen: true))
        {
            foreach (var field in fields) writer.Write(field);
            writer.Flush();
        }
        return buffer.ToArray();
    }
}

/// <summary>
/// Solo configuración servidor: key-id único, revisor GT autenticado y
/// ventana válida. Este registro no contiene claves privadas.
/// </summary>
public sealed record GrandTreasuryTrustedVerificationKey(
    string KeyId,
    string ReviewerSubject,
    string PublicKeyPem,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset ValidUntilUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record AssemblyGrandTreasuryReviewSignature(
    Guid AssemblyId,
    Guid OrganizationId,
    Guid ProposalEventId,
    Guid ReviewEventId,
    string SignerSubject,
    string KeyId,
    DateTimeOffset SignedAtUtc,
    string SignatureBase64);

public sealed record AssemblyGrandTreasuryReviewSignatureResult(
    bool CryptographicallyVerified,
    bool MayAuthorizeSuffrage,
    IReadOnlyList<string> BlockingReasons);
