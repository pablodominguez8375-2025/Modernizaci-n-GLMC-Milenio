using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap.Entities;

namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 — Master-only window open/close and post-import read-back. All three are DISABLED (404) unless
/// <c>InitialLoad:ApplyEnabled</c> is exactly "true". The window transition reuses the existing
/// transactional writer (row lock, audit-first, terminal close). The institution's external approval
/// is referenced by a server-configured 64-hex digest (<c>InitialLoad:ExternalApprovalDigest</c>);
/// the software adds NO per-import legal gate. Responses carry codes and counts only.
/// </summary>
public static class InitialLoadAdminEndpoints
{
    public sealed record ReasonRequest(string? Reason);

    /// <summary>Server-owned evidence. The Master identity is re-verified by the writer before this is read.</summary>
    public sealed class ConfiguredEvidenceSource(IConfiguration configuration, string? reason)
        : IInitialLoadTransitionEvidenceSource
    {
        public Task<InitialLoadTransitionEvidence?> ReadVerifiedAsync(
            InitialLoadAdmissionPolicy.Operation operation, CancellationToken cancellationToken)
        {
            // The institutional authorization is external to the software. What the SERVER can attest is only that the
            // operator configured a well-formed reference (64 lowercase hex) to it; it does not verify its content.
            // Without that reference the transition fails closed (SERVER_EVIDENCE_UNAVAILABLE); nothing else is claimed.
            var digest = configuration["InitialLoad:ExternalApprovalDigest"];
            var referenceConfigured = digest?.Length == 64 && digest.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
            if (!referenceConfigured) return Task.FromResult<InitialLoadTransitionEvidence?>(null);
            var opening = operation == InitialLoadAdmissionPolicy.Operation.RequestOpen;
            var evidence = new InitialLoadAdmissionPolicy.Evidence(
                MasterAccountVerifiedByServer: true, LegalApprovalVerifiedByServer: referenceConfigured,
                AuditSinkVerifiedByServer: true, AuditReason: reason);
            return Task.FromResult<InitialLoadTransitionEvidence?>(
                new InitialLoadTransitionEvidence(evidence, opening ? digest : null));
        }
    }

    public static Task<IResult> OpenAsync(
        HttpContext context, IInstitutionalAccessService access, BootstrapDbContext db,
        IConfiguration configuration, CancellationToken ct) =>
        TransitionAsync(context, access, db, configuration, InitialLoadAdmissionPolicy.Operation.RequestOpen, ct);

    public static Task<IResult> CloseAsync(
        HttpContext context, IInstitutionalAccessService access, BootstrapDbContext db,
        IConfiguration configuration, CancellationToken ct) =>
        TransitionAsync(context, access, db, configuration, InitialLoadAdmissionPolicy.Operation.RequestClose, ct);

    private static async Task<IResult> TransitionAsync(
        HttpContext context, IInstitutionalAccessService access, BootstrapDbContext db,
        IConfiguration configuration, InitialLoadAdmissionPolicy.Operation operation, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!InitialLoadApplyEndpoint.IsEnabled(configuration)) return Results.NotFound();

        string? reason = null;
        try
        {
            using var reader = new StreamReader(context.Request.Body);
            var buffer = new char[4097];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await reader.ReadAsync(buffer.AsMemory(read), ct);
                if (n == 0) break;
                read += n;
            }
            if (read > 4096) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var text = new string(buffer, 0, read);
            reason = JsonSerializer.Deserialize<ReasonRequest>(text,
                new JsonSerializerOptions { MaxDepth = 4, PropertyNameCaseInsensitive = true })?.Reason;
        }
        catch (JsonException) { return Results.BadRequest(new { code = "INVALID_PAYLOAD" }); }

        var result = await new InitialLoadWindowAtomicTransition(db).ExecuteAsync(
            context, access, configuration, new ConfiguredEvidenceSource(configuration, reason), operation, ct);
        if (result.Applied)
            return Results.Ok(new { status = result.Status, version = result.Version, code = result.Code });
        var payload = new { code = result.Code };
        return result.Code switch
        {
            "MASTER_REQUIRED" => Results.Forbid(),
            "SERVER_EVIDENCE_UNAVAILABLE" or "WINDOW_STATUS_UNAVAILABLE" =>
                Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable),
            "WINDOW_CONFLICT" or "WINDOW_WRITE_FAILED" => Results.Conflict(payload),
            _ => Results.UnprocessableEntity(payload)
        };
    }

    /// <summary>Real read-back of what the import created. Counts only; never names, RUT or emails.</summary>
    public static async Task<IResult> ResultAsync(
        HttpContext context, IInstitutionalAccessService access, BootstrapDbContext db, PmgmDbContext core,
        IConfiguration configuration, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!InitialLoadApplyEndpoint.IsEnabled(configuration)) return Results.NotFound();
        var auth = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!auth.Succeeded || auth.Principal is null ||
            !InitialLoadWindowStatusEndpoints.IsRegisteredMaster(auth.Principal, access, configuration))
            return Results.Forbid();

        var window = await InitialLoadWindowStatusReader.ReadAsync(db, ct);
        const string source = InitialLoadMembersStep.Evidence;
        var ledgers = await db.InitialLoadBatchLedgers.AsNoTracking()
            .GroupBy(x => x.Status).Select(g => new { status = g.Key, count = g.Count() }).ToListAsync(ct);
        return Results.Ok(new
        {
            windowStatus = window.Status,
            workshops = await db.Organizations.AsNoTracking().CountAsync(x => x.Origin == InitialLoadWorkshopApplyService.OriginInitialLoad, ct),
            memberships = await core.Memberships.AsNoTracking().CountAsync(x => x.EvidenceReference == source, ct),
            members = await core.Memberships.AsNoTracking().Where(x => x.EvidenceReference == source)
                .Select(x => x.MemberId).Distinct().CountAsync(ct),
            officeAssignments = await core.OfficeAssignments.AsNoTracking().CountAsync(x => x.EvidenceReference == source, ct),
            batches = ledgers
        });
    }
}
