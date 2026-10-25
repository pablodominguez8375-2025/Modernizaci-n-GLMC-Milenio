using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Audit;

/// <summary>
/// Optional QA-only review of an UNTRUSTED, sanitized offline preflight summary.
/// Neither the dataset nor its source SHA is verified here. No import/write exists.
/// </summary>
public static partial class QaSyntheticPlanEndpoints
{
    private const int MaxRequestBytes = 8192;
    private const string DatasetSchema = "PMGM-QA-OPERATIVE-DATASET-v1";
    private static readonly string[] MappedKeys = ["Organization", "Person", "Member", "Membership"];
    private static readonly string[] UnmappedKeys =
        ["offices", "quotas", "cash_receipts", "quota_allocations", "hospitalaria", "meetings", "attendance"];
    private static readonly string[] RootKeys =
        ["schema", "classification", "importStatus", "preflightStatus", "sourceSha256",
         "writesPerformed", "mapped", "unmapped"];

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPattern();

    public static IEndpointRouteBuilder MapQaSyntheticPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/system/qa-synthetic/plan-metadata", HandleAsync)
            .RequireAuthorization()
            .WithTags("Sistema — QA sintético");
        return endpoints;
    }

    // A separate, explicit QA environment AND opt-in are required.
    // The Testing/Development/Production runtimes must not expose this operation.
    public static bool PlanIsEnabled(string environmentName, IConfiguration configuration) =>
        string.Equals(environmentName, "QA", StringComparison.Ordinal) &&
        configuration.GetValue<bool>("QaSyntheticImport:PlanEnabled");

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IInstitutionalAccessService access,
        IHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!PlanIsEnabled(environment.EnvironmentName, configuration))
            return Results.NotFound();

        if (!access.CanConfigureSystem(context.User))
            return Results.Forbid();

        if (!string.Equals(context.Request.ContentType?.Split(';', 2)[0].Trim(),
                "application/json", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        // Bounded streaming read: Content-Length can be absent with chunked input.
        using var input = new MemoryStream();
        var buffer = new byte[1024];
        while (true)
        {
            var count = await context.Request.Body.ReadAsync(buffer, cancellationToken);
            if (count == 0) break;
            if (input.Length + count > MaxRequestBytes)
                return Reject("BODY_TOO_LARGE");
            input.Write(buffer, 0, count);
        }

        try
        {
            using var document = JsonDocument.Parse(input.ToArray(),
                new JsonDocumentOptions { MaxDepth = 5 });
            if (!TrySummarize(document.RootElement, out var summary))
                return Reject("INVALID_PLAN_METADATA");

            return Results.Ok(new
            {
                status = "REVIEW_ONLY",
                sourceSha256 = summary!.SourceSha256,
                mapped = summary.Mapped,
                notMapped = summary.NotMapped,
                sourceVerification = "UNTRUSTED_CLIENT_SUMMARY",
                needsIndependentDatasetVerification = true,
                canApply = false,
                writesPerformed = 0
            });
        }
        catch (JsonException)
        {
            return Reject("INVALID_JSON");
        }
    }

    private static IResult Reject(string errorCode) =>
        Results.BadRequest(new { errorCode, writesPerformed = 0 });

    // Exposed for side-effect-free contract tests. Never returns row data.
    public static bool TrySummarize(JsonElement root, out QaSyntheticPlanSummary? summary)
    {
        summary = null;
        if (!HasExactKeys(root, RootKeys) ||
            !EqualsString(root, "schema", DatasetSchema) ||
            !EqualsString(root, "classification", "SYNTHETIC_ONLY") ||
            !EqualsString(root, "importStatus", "NOT_IMPORTED") ||
            !EqualsString(root, "preflightStatus", "READY_FOR_MAPPING_ONLY") ||
            !TryInteger(root.GetProperty("writesPerformed"), 0, 0, out _) ||
            !TryString(root, "sourceSha256", out var sha) ||
            !DigestPattern().IsMatch(sha) ||
            !HasExactKeys(root.GetProperty("mapped"), MappedKeys) ||
            !HasExactKeys(root.GetProperty("unmapped"), UnmappedKeys))
            return false;

        var mapped = root.GetProperty("mapped");
        var unmapped = root.GetProperty("unmapped");
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in MappedKeys)
        {
            if (!TryInteger(mapped.GetProperty(key), 0, 2000, out var count))
                return false;
            counts[key] = count;
        }
        if (counts["Organization"] is < 2 or > 30 ||
            counts["Person"] is < 1 or > 2000 ||
            counts["Person"] != counts["Member"] ||
            counts["Person"] != counts["Membership"])
            return false;

        var ignored = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in UnmappedKeys)
        {
            if (!TryInteger(unmapped.GetProperty(key), 0, 100000, out var count))
                return false;
            ignored[key] = count;
        }

        // An untrusted client's summary is NEVER sufficient evidence to import.
        summary = new QaSyntheticPlanSummary(sha, counts, ignored);
        return true;
    }

    private static bool HasExactKeys(JsonElement root, string[] required)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;
        var present = new HashSet<string>(StringComparer.Ordinal);
        var allowed = required.ToHashSet(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !present.Add(property.Name))
                return false; // No duplicates, casing variants or PII extensions.
        }
        return present.SetEquals(allowed);
    }

    private static bool EqualsString(JsonElement root, string key, string expected) =>
        TryString(root, key, out var value) && string.Equals(value, expected, StringComparison.Ordinal);

    private static bool TryString(JsonElement root, string key, out string result)
    {
        result = string.Empty;
        var value = root.GetProperty(key);
        if (value.ValueKind != JsonValueKind.String) return false;
        result = value.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryInteger(JsonElement value, int min, int max, out int result)
    {
        result = 0;
        if (value.ValueKind != JsonValueKind.Number) return false;
        var raw = value.GetRawText();
        // Reject 1.0, 1e0, negative zero and other numeric coercions.
        if (raw.Length == 0 || raw.Length > 10 ||
            (raw.Length > 1 && raw[0] == '0') ||
            raw.Any(ch => ch is < '0' or > '9') ||
            !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out result))
            return false;
        return result >= min && result <= max;
    }

    public sealed record QaSyntheticPlanSummary(
        string SourceSha256,
        IReadOnlyDictionary<string, int> Mapped,
        IReadOnlyDictionary<string, int> NotMapped);
}
