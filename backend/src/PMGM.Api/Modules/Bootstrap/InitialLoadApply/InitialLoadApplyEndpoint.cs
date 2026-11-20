using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 — authenticated Master-only apply boundary. DISABLED by default (config
/// <c>InitialLoad:ApplyEnabled</c> must be exactly "true", otherwise 404). The batch fingerprint is
/// ALWAYS recomputed here from the received CSV bytes with a server-held HMAC secret and bound to a
/// still-valid preflight receipt; nothing the client sends is trusted as a fingerprint. Actor and
/// correlation come from the authenticated principal / request, never from the CSV or body.
/// Responses carry codes and counts only. Real data still requires legal sign-off and a separate GO.
/// </summary>
public static class InitialLoadApplyEndpoint
{
    private const int MaxRequestBytes = 2_500_000;

    public static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration["InitialLoad:ApplyEnabled"], "true", StringComparison.Ordinal);

    public static byte[]? ReceiptSecret(IConfiguration configuration)
    {
        var raw = configuration["InitialLoad:ReceiptSecret"];
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try { var bytes = Convert.FromBase64String(raw); return bytes.Length >= 32 ? bytes : null; }
        catch (FormatException) { return null; }
    }

    public sealed record Request(Dictionary<string, string>? Files, string? Receipt);

    public static async Task<IResult> ApplyAsync(
        HttpContext context,
        IInstitutionalAccessService access,
        BootstrapDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.Pragma = "no-cache";
        if (!IsEnabled(configuration)) return Results.NotFound();

        var authentication = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null ||
            !InitialLoadWindowStatusEndpoints.IsRegisteredMaster(authentication.Principal, access, configuration))
            return Results.Forbid();

        // The audited actor is the verified JWT subject of the registered Master.
        var actor = authentication.Principal.FindAll(ClaimTypes.NameIdentifier).Select(x => x.Value).Distinct().ToArray();
        if (actor.Length != 1 || string.IsNullOrWhiteSpace(actor[0])) return Results.Forbid();

        var secret = ReceiptSecret(configuration);
        if (secret is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var window = await InitialLoadWindowStatusReader.ReadAsync(db, cancellationToken);
        if (window.Status == InitialLoadWindowStatusReader.Unavailable)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (window.Status != PMGM.Api.Modules.Bootstrap.Entities.InitialLoadWindow.Open)
            return Results.Conflict(new { code = "WINDOW_NOT_OPEN" });

        if (context.Request.ContentType is null ||
            !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (context.Request.ContentLength is > MaxRequestBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        await using var body = new MemoryStream();
        var buffer = new byte[16_384];
        while (true)
        {
            var read = await context.Request.Body.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (body.Length + read > MaxRequestBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            body.Write(buffer, 0, read);
        }

        var outcome = await ApplyVerifiedAsync(
            actor[0], context.TraceIdentifier, body.ToArray(), secret, db, DateTimeOffset.UtcNow, cancellationToken);
        return Results.Json(outcome.Payload, statusCode: outcome.Status);
    }

    public sealed record Outcome(int Status, object Payload);

    /// <summary>
    /// Everything after authentication and body reading. The fingerprint is recomputed here from the
    /// received bytes (never accepted from the client). Exposed for tests with a trusted actor.
    /// </summary>
    public static async Task<Outcome> ApplyVerifiedAsync(
        string actor, string correlationId, byte[] body, byte[] secret,
        BootstrapDbContext db, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        Request? request;
        try
        {
            request = JsonSerializer.Deserialize<Request>(body,
                new JsonSerializerOptions { MaxDepth = 4, PropertyNameCaseInsensitive = true });
        }
        catch (JsonException) { return new Outcome(400, new { code = "INVALID_APPLY_PAYLOAD" }); }
        if (request?.Files is null || string.IsNullOrWhiteSpace(request.Receipt))
            return new Outcome(400, new { code = "INVALID_APPLY_PAYLOAD" });

        // Server-side HMAC over the exact received bytes, bound to an unexpired preflight receipt.
        var verification = InitialLoadPreflightReceiptPolicy.Verify(request.Files, request.Receipt, secret, nowUtc);
        if (!verification.Valid || verification.BatchFingerprint is null)
            return new Outcome(422, new { code = verification.Code });

        var sheets = InitialLoadCsvPreviewPolicy.TryParseSheets(request.Files);
        var applicability = InitialLoadApplicabilityPolicy.Inspect(sheets);
        if (!applicability.Applicable)
            return new Outcome(422, new { code = "BATCH_NOT_APPLICABLE", blockers = applicability.Blockers.Take(100) });
        var plan = InitialLoadPlanAdapter.Build(sheets).Plan!;

        var result = await new InitialLoadApplyOrchestrator(db).ApplyAsync(
            true, verification.BatchFingerprint, plan.Workshops, plan.Members, plan.Offices,
            actor, correlationId, cancellationToken);

        var payload = new
        {
            applied = result.Applied, alreadyApplied = result.AlreadyApplied, code = result.Code,
            counts = new
            {
                workshopsCreated = result.WorkshopsCreated, membersCreated = result.MembersCreated,
                memberExactSkips = result.MemberExactSkips, officesCreated = result.OfficesCreated,
                officeExactSkips = result.OfficeExactSkips
            }
        };
        if (result.Applied || result.AlreadyApplied) return new Outcome(200, payload);
        return result.Code switch
        {
            "WINDOW_NOT_OPEN" or "CONCURRENT_OR_CONFLICTING_BATCH" => new Outcome(409, payload),
            "WINDOW_STATUS_UNAVAILABLE" => new Outcome(503, payload),
            _ => new Outcome(422, payload)
        };
    }
}
