using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Offline/server-internal preflight receipt for #694. No HTTP endpoint, DI
/// registration, persistence, audit write, legal admission or CSV apply.
/// Always treat the receipt as proof of bytes checked, NEVER permission to
/// import members. The real server must hold a random key outside source
/// control and must never include it in logs, API payloads or exports.
/// </summary>
public static class InitialLoadPreflightReceiptPolicy
{
    private const string BatchDomain = "PMGM.initial-load.csv-batch.v1";
    private const string ReceiptDomain = "PMGM.initial-load.preflight-receipt.v1";
    public static readonly TimeSpan Validity = TimeSpan.FromMinutes(15);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public sealed record Receipt(
        string Token,
        string BatchFingerprint,
        DateTimeOffset ExpiresAtUtc,
        bool CanApply = false,
        int WritesPerformed = 0);

    public sealed record Verification(
        bool Valid,
        string Code,
        string? BatchFingerprint,
        bool CanApply = false,
        int WritesPerformed = 0);

    /// <summary>
    /// Call only for already authorized synthetic/test data until juridical
    /// admission is implemented. The secret is injected by a trusted server
    /// source; no default or hardcoded secret is accepted.
    /// </summary>
    public static Receipt? Issue(
        IReadOnlyDictionary<string, string>? csvFiles,
        byte[]? serverSecret,
        DateTimeOffset nowUtc)
    {
        if (!HasValidSecret(serverSecret) ||
            nowUtc.Offset != TimeSpan.Zero || csvFiles is null ||
            !PassesPreflight(csvFiles))
            return null;

        var digest = Fingerprint(csvFiles, serverSecret!);
        if (digest is null) return null;
        var issuedAt = nowUtc.ToUnixTimeSeconds();
        // CSPRNG nonce prevents duplicate independent previews even if their
        // timestamp and file content are identical; not a replay-prevention DB.
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            .ToLowerInvariant();
        var signature = Sign(issuedAt, nonce, digest, serverSecret!);
        var token = $"v1.{issuedAt.ToString(CultureInfo.InvariantCulture)}.{nonce}.{signature}";
        return new Receipt(token, digest, nowUtc.Add(Validity));
    }

    public static Verification Verify(
        IReadOnlyDictionary<string, string>? csvFiles,
        string? token,
        byte[]? serverSecret,
        DateTimeOffset nowUtc)
    {
        static Verification Deny(string code) => new(false, code, null);

        if (!HasValidSecret(serverSecret))
            return Deny("SERVER_SECRET_UNAVAILABLE");
        if (nowUtc.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(token))
            return Deny("INVALID_RECEIPT");

        var segments = token.Split('.');
        if (segments.Length != 4 || segments[0] != "v1" ||
            segments[1].Length is < 1 or > 16 ||
            !segments[1].All(c => c is >= '0' and <= '9') ||
            !long.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture,
                out var seconds) ||
            !LowerHex(segments[2], 32) || !LowerHex(segments[3], 64))
            return Deny("INVALID_RECEIPT");

        DateTimeOffset issuedAt;
        try { issuedAt = DateTimeOffset.FromUnixTimeSeconds(seconds); }
        catch (ArgumentOutOfRangeException) { return Deny("INVALID_RECEIPT"); }

        // A preview cannot be pre-dated/future-dated, and expiry is inclusive.
        // No grace time is implied. The caller must pass the trusted server clock.
        if (issuedAt > nowUtc || nowUtc - issuedAt > Validity)
            return Deny("RECEIPT_EXPIRED");

        if (csvFiles is null || !PassesPreflight(csvFiles))
            return Deny("INVALID_PREFLIGHT");

        var fingerprint = Fingerprint(csvFiles, serverSecret!);
        if (fingerprint is null) return Deny("INVALID_PREFLIGHT");
        var expected = Sign(seconds, segments[2], fingerprint, serverSecret!);
        // Compare MAC bytes in constant time (no short-circuit prefix compare).
        var suppliedBytes = Convert.FromHexString(segments[3]);
        var expectedBytes = Convert.FromHexString(expected);
        if (!CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes))
            return Deny("INVALID_RECEIPT");

        // Valid means exactly "same bytes as a passing, unexpired preflight".
        // This is NOT legal approval, an audit record, or a one-time ticket.
        return new Verification(true, "PREFLIGHT_RECEIPT_VALID", fingerprint);
    }

    private static bool PassesPreflight(
        IReadOnlyDictionary<string, string> csvFiles)
    {
        var preview = InitialLoadCsvPreviewPolicy.Inspect(csvFiles);
        // Header-only package is not an initial-load batch. A workshop and
        // a person are mandatory; an office row can be absent.
        return preview.Valid &&
               preview.DataRowCounts.TryGetValue("Talleres.csv", out var workshops) &&
               workshops > 0 &&
               preview.DataRowCounts.TryGetValue("Hermanos.csv", out var members) &&
               members > 0;
    }

    private static string? Fingerprint(
        IReadOnlyDictionary<string, string> csvFiles, byte[] serverSecret)
    {
        try
        {
            using var hash = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, serverSecret);
            hash.AppendData(Encoding.ASCII.GetBytes(BatchDomain));
            foreach (var sheet in InitialLoadTemplateEndpoints.Sheets)
            {
                var fileName = StrictUtf8.GetBytes(sheet.Name);
                var content = StrictUtf8.GetBytes(csvFiles[sheet.Name]);
                AppendFrame(hash, fileName);
                AppendFrame(hash, content);
            }
            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }
        catch (EncoderFallbackException)
        {
            // Do not silently conflate distinct CSV byte sequences via the
            // default UTF-8 replacement character.
            return null;
        }
    }

    private static void AppendFrame(IncrementalHash hash, byte[] bytes)
    {
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, bytes.Length);
        hash.AppendData(size);
        hash.AppendData(bytes);
    }

    private static string Sign(long issuedAt, string nonce, string fingerprint,
        byte[] serverSecret)
    {
        var payload = ReceiptDomain + "|" +
            issuedAt.ToString(CultureInfo.InvariantCulture) + "|" +
            nonce + "|" + fingerprint;
        return Convert.ToHexString(
            HMACSHA256.HashData(serverSecret, Encoding.ASCII.GetBytes(payload)))
            .ToLowerInvariant();
    }

    private static bool HasValidSecret(byte[]? secret) =>
        secret is { Length: >= 32 };

    private static bool LowerHex(string text, int length) =>
        text.Length == length &&
        text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
