using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Notifications;
using PMGM.Api.Modules.Notifications.Entities;
using PMGM.Api.Modules.UserAccounts;

namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// «Corregir como soporte» (#574, etapa 2). Permiso EXPRESO y con vigencia que solo el Máster registrado da a un
/// Superusuario de soporte; con él, el Superusuario puede corregir un dato de contacto de un Hermano, con motivo
/// obligatorio y auditoría con el nombre real del operador. El Hermano afectado ve la corrección en
/// «Privacidad y mis datos». Lista cerrada de campos: nunca Tesorería, Hospitalaria, firmas ni claves.
/// </summary>
public sealed class SupportCorrectionGrant
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OperatorSubject { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string GrantedBySubject { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class SupportCorrection
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid GrantId { get; set; }
    public Guid MemberId { get; set; }
    public string OperatorSubject { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    /// <summary>Complemento visible para el Hermano (p. ej. el título del aviso reenviado). Nunca el valor de un dato personal.</summary>
    public string? Detail { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public static class SupportCorrectionPolicy
{
    public static readonly TimeSpan MaxGrantLifetime = TimeSpan.FromHours(4);
    public static readonly TimeSpan DefaultGrantLifetime = TimeSpan.FromHours(1);
    /// <summary>Lista cerrada de datos que soporte puede corregir: solo contacto.</summary>
    public static readonly string[] Fields = ["email", "phone", "address"];
    public const string ResendField = "resend_notice";
    /// <summary>Un mismo aviso no se reenvía más de una vez cada 10 minutos.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(10);
    public static string Label(string field) => field switch
    {
        "email" => "Correo electrónico", "phone" => "Teléfono", "address" => "Domicilio", ResendField => "Aviso reenviado", _ => field
    };
    public static bool ValidReason(string? reason) => reason?.Trim().Length is >= 10 and <= 500;
}

public static class SupportCorrectionEndpoints
{
    public sealed record GrantRequest(string? OperatorSubject, string? Reason, int? Minutes);
    public sealed record ResendRequest(string? TargetSubject, Guid? MessageId, string? Reason);
    public sealed record CorrectRequest(string? TargetSubject, string? Field, string? NewValue, string? Reason);

    public static IEndpointRouteBuilder MapSupportCorrectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system/support/corrections").RequireAuthorization().WithTags("Sistema — Corregir como soporte");
        group.MapGet("", GetStatus);
        group.MapPost("/grants", CreateGrant);
        group.MapPost("/grants/{grantId:guid}/revoke", RevokeGrant);
        group.MapPost("", Correct);
        group.MapGet("/notices", ListNotices);
        group.MapPost("/resend", ResendNotice);
        endpoints.MapGet("/api/session/support-corrections", MyCorrections).RequireAuthorization().WithTags("Privacidad");
        return endpoints;
    }

    private static bool Authorized(HttpContext ctx, IInstitutionalAccessService access)
        => access.CanConfigureSystem(ctx.User) && access.IsPlatformSuperAdmin(ctx.User) &&
           string.IsNullOrEmpty(ctx.User.FindFirstValue(SupportViewAsClaims.Operator));
    private static string Subject(HttpContext ctx) => ctx.User.FindFirstValue("sub") ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    private static string DisplayName(HttpContext ctx) => ctx.User.FindFirstValue("name") ?? ctx.User.FindFirstValue("preferred_username") ?? Subject(ctx);
    private static string? Normalize(string? v) { var t = v?.Trim(); return string.IsNullOrWhiteSpace(t) ? null : t; }

    private static async Task<IResult> GetStatus(HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IConfiguration configuration, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!Authorized(ctx, access)) return Results.Forbid();
        var me = Subject(ctx); var now = DateTimeOffset.UtcNow;
        var isMaster = InitialLoadWindowStatusEndpoints.IsRegisteredMaster(ctx.User, access, configuration);
        var q = db.SupportCorrectionGrants.AsNoTracking().Where(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now);
        if (!isMaster) q = q.Where(x => x.OperatorSubject == me);
        var grants = await q.OrderBy(x => x.ExpiresAtUtc).Select(x => new
        { grantId = x.Id, operatorSubject = x.OperatorSubject, operatorName = x.OperatorName, reason = x.Reason, expiresAtUtc = x.ExpiresAtUtc }).ToListAsync(ct);
        return Results.Ok(new
        {
            isMaster, canCorrect = !isMaster && grants.Count > 0, grants,
            maxHours = (int)SupportCorrectionPolicy.MaxGrantLifetime.TotalHours,
            fields = SupportCorrectionPolicy.Fields.Select(f => new { field = f, label = SupportCorrectionPolicy.Label(f) })
        });
    }

    private static async Task<IResult> CreateGrant(GrantRequest request, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAccountIdentityProvider identity, IAuditService audit, IConfiguration configuration, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!Authorized(ctx, access)) return Results.Forbid();
        async Task<IResult> Reject(int status, string code, string message)
        {
            audit.Add(ctx, "support.correction_grant.denied", "SupportCorrectionGrant", "none", null, AuditResults.Rejected, new { code });
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Json(new { code, message }, statusCode: status);
        }
        if (!InitialLoadWindowStatusEndpoints.IsRegisteredMaster(ctx.User, access, configuration))
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_CORRECTION_MASTER_REQUIRED", "Solo el Máster registrado puede dar este permiso.");
        var target = request.OperatorSubject?.Trim() ?? ""; var reason = request.Reason?.Trim() ?? "";
        if (!Guid.TryParse(target, out _)) return Results.BadRequest(new { code = "SUPPORT_CORRECTION_OPERATOR_REQUIRED", message = "Seleccione al Superusuario." });
        if (!SupportCorrectionPolicy.ValidReason(reason))
            return Results.BadRequest(new { code = "SUPPORT_CORRECTION_REASON_REQUIRED", message = "Indique el motivo (10 a 500 caracteres)." });
        var span = request.Minutes is null ? SupportCorrectionPolicy.DefaultGrantLifetime : TimeSpan.FromMinutes(request.Minutes.Value);
        if (span < TimeSpan.FromMinutes(5) || span > SupportCorrectionPolicy.MaxGrantLifetime)
            return Results.BadRequest(new { code = "SUPPORT_CORRECTION_DURATION_INVALID", message = "La vigencia debe estar entre 5 minutos y 4 horas." });
        if (string.Equals(target, Subject(ctx), StringComparison.Ordinal))
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_CORRECTION_SELF_DENIED", "El Máster no se da el permiso a sí mismo.");
        if (!identity.Configured) return Results.Json(new { code = "IDENTITY_UNAVAILABLE", message = "La identidad no está disponible; reintente más tarde." }, statusCode: 503);
        AccountClaims? claims;
        try { claims = await identity.GetClaimsAsync(target, ct); }
        catch (Exception e) when (e is AccountDeliveryException or HttpRequestException or TaskCanceledException)
        { return Results.Json(new { code = "IDENTITY_UNAVAILABLE", message = "La identidad no está disponible; reintente más tarde." }, statusCode: 503); }
        if (claims is null || !claims.Enabled || !string.Equals(claims.Kind, "superuser", StringComparison.OrdinalIgnoreCase))
            return await Reject(StatusCodes.Status404NotFound, "SUPPORT_CORRECTION_OPERATOR_INVALID", "El permiso solo se da a un Superusuario de soporte habilitado.");

        var now = DateTimeOffset.UtcNow;
        foreach (var old in await db.SupportCorrectionGrants.Where(x => x.OperatorSubject == target && x.RevokedAtUtc == null && x.ExpiresAtUtc > now).ToListAsync(ct))
            old.RevokedAtUtc = now;
        var grant = new SupportCorrectionGrant { OperatorSubject = target, OperatorName = claims.Name, GrantedBySubject = Subject(ctx), Reason = reason, StartedAtUtc = now, ExpiresAtUtc = now + span };
        db.SupportCorrectionGrants.Add(grant);
        audit.Add(ctx, "support.correction_grant.created", "SupportCorrectionGrant", grant.Id.ToString("D"), null, AuditResults.Success,
            new { operatorSubject = target, operatorName = claims.Name, reason, expiresAtUtc = grant.ExpiresAtUtc });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { grantId = grant.Id, operatorSubject = target, operatorName = claims.Name, reason, expiresAtUtc = grant.ExpiresAtUtc });
    }

    private static async Task<IResult> RevokeGrant(Guid grantId, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAuditService audit, IConfiguration configuration, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!Authorized(ctx, access) || !InitialLoadWindowStatusEndpoints.IsRegisteredMaster(ctx.User, access, configuration)) return Results.Forbid();
        var grant = await db.SupportCorrectionGrants.FirstOrDefaultAsync(x => x.Id == grantId, ct);
        if (grant is null) return Results.NotFound();
        if (grant.RevokedAtUtc is null)
        {
            grant.RevokedAtUtc = DateTimeOffset.UtcNow;
            audit.Add(ctx, "support.correction_grant.revoked", "SupportCorrectionGrant", grant.Id.ToString("D"), null, AuditResults.Success, new { operatorSubject = grant.OperatorSubject });
            await db.SaveChangesAsync(ct);
        }
        return Results.Ok(new { revoked = true });
    }

    private static async Task<IResult> Correct(CorrectRequest request, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAccountIdentityProvider identity, IAuditService audit, IConfiguration configuration, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        if (!Authorized(ctx, access)) return Results.Forbid();
        var me = Subject(ctx); var now = DateTimeOffset.UtcNow;
        async Task<IResult> Reject(int status, string code, string message)
        {
            audit.Add(ctx, "support.correction.denied", "SupportCorrection", "none", null, AuditResults.Rejected, new { code });
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Json(new { code, message }, statusCode: status);
        }
        if (InitialLoadWindowStatusEndpoints.IsRegisteredMaster(ctx.User, access, configuration))
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_CORRECTION_MASTER_DENIED", "El Máster da el permiso, pero no corrige datos.");
        var grant = await db.SupportCorrectionGrants.AsNoTracking()
            .Where(x => x.OperatorSubject == me && x.RevokedAtUtc == null && x.ExpiresAtUtc > now).OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(ct);
        if (grant is null)
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_CORRECTION_PERMISSION_REQUIRED", "No tiene un permiso vigente del Máster para corregir datos.");
        var field = request.Field?.Trim().ToLowerInvariant() ?? ""; var reason = request.Reason?.Trim() ?? "";
        var target = request.TargetSubject?.Trim() ?? "";
        if (!SupportCorrectionPolicy.Fields.Contains(field))
            return await Reject(StatusCodes.Status400BadRequest, "SUPPORT_CORRECTION_FIELD_DENIED", "Solo se puede corregir correo, teléfono o domicilio.");
        if (!SupportCorrectionPolicy.ValidReason(reason))
            return Results.BadRequest(new { code = "SUPPORT_CORRECTION_REASON_REQUIRED", message = "Indique el motivo (10 a 500 caracteres)." });
        var value = Normalize(request.NewValue);
        if (field == "email" && (value is null || value.Length > 320 || value.IndexOf('@') is var at && (at <= 0 || at >= value.Length - 1 || value.IndexOf('.', at) <= at + 1)))
            return Results.BadRequest(new { code = "SUPPORT_CORRECTION_VALUE_INVALID", message = "El correo electrónico no tiene un formato válido." });
        if (field == "phone" && value is { Length: > 80 }) return Results.BadRequest(new { code = "SUPPORT_CORRECTION_VALUE_INVALID", message = "El teléfono supera el máximo permitido." });
        if (field == "address" && value is { Length: > 500 }) return Results.BadRequest(new { code = "SUPPORT_CORRECTION_VALUE_INVALID", message = "El domicilio supera el máximo permitido." });
        if (!Guid.TryParse(target, out _)) return Results.BadRequest(new { code = "SUPPORT_CORRECTION_TARGET_REQUIRED", message = "Seleccione al Hermano." });
        if (!identity.Configured) return Results.Json(new { code = "IDENTITY_UNAVAILABLE", message = "La identidad no está disponible; reintente más tarde." }, statusCode: 503);
        AccountClaims? claims;
        try { claims = await identity.GetClaimsAsync(target, ct); }
        catch (Exception e) when (e is AccountDeliveryException or HttpRequestException or TaskCanceledException)
        { return Results.Json(new { code = "IDENTITY_UNAVAILABLE", message = "La identidad no está disponible; reintente más tarde." }, statusCode: 503); }
        if (claims is null || !claims.Enabled || SupportViewAsClaims.IsPrivileged(claims))
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_CORRECTION_TARGET_DENIED", "Solo se corrigen datos de Hermanos con cuenta habilitada, no de cuentas de administración.");
        var memberId = await db.Database.SqlQuery<Guid>(
            $"SELECT \"MemberId\" AS \"Value\" FROM core.member_identity_links WHERE \"Subject\" = {target} AND \"RevokedAtUtc\" IS NULL LIMIT 1").FirstOrDefaultAsync(ct);
        var member = memberId == Guid.Empty ? null : await db.Members.Include(x => x.Person).FirstOrDefaultAsync(x => x.Id == memberId, ct);
        if (member is null) return await Reject(StatusCodes.Status404NotFound, "SUPPORT_CORRECTION_MEMBER_NOT_FOUND", "La cuenta no está vinculada a una ficha de Hermano.");
        var person = member.Person;
        var current = field switch { "email" => person.Email, "phone" => person.Phone, _ => person.Address };
        if (string.Equals(current, value, StringComparison.Ordinal))
            return Results.Ok(new { status = "unchanged" });
        switch (field) { case "email": person.Email = value; break; case "phone": person.Phone = value; break; default: person.Address = value; break; }
        var operatorName = grant.OperatorName;
        var correction = new SupportCorrection { GrantId = grant.Id, MemberId = member.Id, OperatorSubject = me, OperatorName = operatorName, Field = field, Reason = reason, CreatedAtUtc = now };
        db.SupportCorrections.Add(correction);
        // La auditoría guarda al operador real y el campo, nunca el valor anterior ni el nuevo.
        audit.Add(ctx, "support.correction.applied", "member", member.Id.ToString("D"), null, AuditResults.Success,
            new { correctionId = correction.Id, field, reason, operatorName, grantId = grant.Id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { status = "corrected", correctionId = correction.Id, field, label = SupportCorrectionPolicy.Label(field) });
    }

    /// <summary>Operador con permiso vigente (nunca el Máster) y Hermano objetivo habilitado; resuelve su ficha.</summary>
    private sealed record Resolved(SupportCorrectionGrant Grant, Guid MemberId, string Subject);
    private static async Task<(Resolved? Ok, IResult? Error)> ResolveAsync(string rawTarget, HttpContext ctx, PmgmDbContext db, IInstitutionalAccessService access,
        IAccountIdentityProvider identity, IAuditService audit, IConfiguration configuration, string deniedAction, CancellationToken ct)
    {
        if (!Authorized(ctx, access)) return (null, Results.Forbid());
        var me = Subject(ctx); var now = DateTimeOffset.UtcNow;
        async Task<(Resolved?, IResult?)> Reject(int status, string code, string message)
        {
            audit.Add(ctx, deniedAction, "SupportCorrection", "none", null, AuditResults.Rejected, new { code });
            await db.SaveChangesAsync(CancellationToken.None);
            return (null, Results.Json(new { code, message }, statusCode: status));
        }
        if (InitialLoadWindowStatusEndpoints.IsRegisteredMaster(ctx.User, access, configuration))
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_CORRECTION_MASTER_DENIED", "El Máster da el permiso, pero no corrige datos.");
        var grant = await db.SupportCorrectionGrants.AsNoTracking()
            .Where(x => x.OperatorSubject == me && x.RevokedAtUtc == null && x.ExpiresAtUtc > now).OrderByDescending(x => x.StartedAtUtc).FirstOrDefaultAsync(ct);
        if (grant is null)
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_CORRECTION_PERMISSION_REQUIRED", "No tiene un permiso vigente del Máster para corregir datos.");
        var target = rawTarget.Trim();
        if (!Guid.TryParse(target, out _)) return (null, Results.BadRequest(new { code = "SUPPORT_CORRECTION_TARGET_REQUIRED", message = "Seleccione al Hermano." }));
        if (!identity.Configured) return (null, Results.Json(new { code = "IDENTITY_UNAVAILABLE", message = "La identidad no está disponible; reintente más tarde." }, statusCode: 503));
        AccountClaims? claims;
        try { claims = await identity.GetClaimsAsync(target, ct); }
        catch (Exception e) when (e is AccountDeliveryException or HttpRequestException or TaskCanceledException)
        { return (null, Results.Json(new { code = "IDENTITY_UNAVAILABLE", message = "La identidad no está disponible; reintente más tarde." }, statusCode: 503)); }
        if (claims is null || !claims.Enabled || SupportViewAsClaims.IsPrivileged(claims))
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_CORRECTION_TARGET_DENIED", "Solo se atienden Hermanos con cuenta habilitada, no cuentas de administración.");
        var memberId = await db.Database.SqlQuery<Guid>(
            $"SELECT \"MemberId\" AS \"Value\" FROM core.member_identity_links WHERE \"Subject\" = {target} AND \"RevokedAtUtc\" IS NULL LIMIT 1").FirstOrDefaultAsync(ct);
        if (memberId == Guid.Empty) return await Reject(StatusCodes.Status404NotFound, "SUPPORT_CORRECTION_MEMBER_NOT_FOUND", "La cuenta no está vinculada a una ficha de Hermano.");
        return (new Resolved(grant, memberId, target), null);
    }

    /// <summary>Avisos recientes del Hermano que soporte puede reenviar: solo clasificación interna y con correo registrado.</summary>
    private static async Task<IResult> ListNotices(string? targetSubject, HttpContext ctx, PmgmDbContext db, NotificationDbContext notifications,
        IInstitutionalAccessService access, IAccountIdentityProvider identity, IAuditService audit, IConfiguration configuration, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        var (ok, error) = await ResolveAsync(targetSubject ?? "", ctx, db, access, identity, audit, configuration, "support.resend.denied", ct);
        if (error is not null) return error;
        var rows = await notifications.NotificationMessages.AsNoTracking()
            .Where(x => x.RecipientSubject == ok!.Subject && x.Classification == NotificationCodes.Classification.Internal && x.RecipientEmail != null)
            .OrderByDescending(x => x.CreatedAtUtc).Take(20)
            .Select(x => new
            {
                messageId = x.Id, typeCode = x.TypeCode, subject = x.Subject, createdAtUtc = x.CreatedAtUtc,
                emailStatus = x.Deliveries.Where(d => d.Channel == NotificationCodes.Channel.Email).OrderByDescending(d => d.CreatedAtUtc).Select(d => d.Status).FirstOrDefault()
            }).ToListAsync(ct);
        return Results.Ok(new { items = rows });
    }

    /// <summary>Reenvía un aviso ya emitido al MISMO correo registrado; no cambia contenido ni destino.</summary>
    private static async Task<IResult> ResendNotice(ResendRequest request, HttpContext ctx, PmgmDbContext db, NotificationDbContext notifications,
        IInstitutionalAccessService access, IAccountIdentityProvider identity, IAuditService audit, IConfiguration configuration, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        var reason = request.Reason?.Trim() ?? "";
        if (!SupportCorrectionPolicy.ValidReason(reason))
            return Results.BadRequest(new { code = "SUPPORT_CORRECTION_REASON_REQUIRED", message = "Indique el motivo (10 a 500 caracteres)." });
        if (request.MessageId is null) return Results.BadRequest(new { code = "SUPPORT_RESEND_NOTICE_REQUIRED", message = "Seleccione el aviso." });
        var (ok, error) = await ResolveAsync(request.TargetSubject ?? "", ctx, db, access, identity, audit, configuration, "support.resend.denied", ct);
        if (error is not null) return error;
        async Task<IResult> Reject(int status, string code, string message)
        {
            audit.Add(ctx, "support.resend.denied", "SupportCorrection", "none", null, AuditResults.Rejected, new { code });
            await db.SaveChangesAsync(CancellationToken.None);
            return Results.Json(new { code, message }, statusCode: status);
        }
        var message = await notifications.NotificationMessages.Include(x => x.Deliveries).FirstOrDefaultAsync(x => x.Id == request.MessageId, ct);
        if (message is null || message.RecipientSubject != ok!.Subject || string.IsNullOrWhiteSpace(message.RecipientEmail))
            return await Reject(StatusCodes.Status404NotFound, "SUPPORT_RESEND_NOTICE_NOT_FOUND", "El aviso no existe o no es de este Hermano.");
        if (message.Classification != NotificationCodes.Classification.Internal)
            return await Reject(StatusCodes.Status403Forbidden, "SUPPORT_RESEND_NOTICE_PROTECTED", "Este aviso es reservado y soporte no puede reenviarlo.");
        var now = DateTimeOffset.UtcNow;
        if (message.Deliveries.Any(d => d.Channel == NotificationCodes.Channel.Email && d.CreatedAtUtc > now - SupportCorrectionPolicy.ResendCooldown))
            return await Reject(StatusCodes.Status429TooManyRequests, "SUPPORT_RESEND_TOO_SOON", "Este aviso se envió hace muy poco. Espere 10 minutos antes de reenviarlo.");
        notifications.NotificationDeliveries.Add(new NotificationDelivery
        {
            NotificationMessageId = message.Id, Channel = NotificationCodes.Channel.Email, Status = NotificationCodes.DeliveryStatus.Queued, ScheduledAtUtc = now
        });
        await notifications.SaveChangesAsync(ct);
        var detail = message.Subject.Length > 200 ? message.Subject[..200] : message.Subject;
        var record = new SupportCorrection
        {
            GrantId = ok.Grant.Id, MemberId = ok.MemberId, OperatorSubject = Subject(ctx), OperatorName = ok.Grant.OperatorName,
            Field = SupportCorrectionPolicy.ResendField, Reason = reason, Detail = detail, CreatedAtUtc = now
        };
        db.SupportCorrections.Add(record);
        audit.Add(ctx, "support.resend.applied", "NotificationMessage", message.Id.ToString("D"), null, AuditResults.Success,
            new { correctionId = record.Id, typeCode = message.TypeCode, reason, operatorName = ok.Grant.OperatorName, grantId = ok.Grant.Id });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { status = "queued", correctionId = record.Id, label = SupportCorrectionPolicy.Label(SupportCorrectionPolicy.ResendField) });
    }

    /// <summary>Lo que el propio Hermano ve en «Privacidad y mis datos»: qué se corrigió, cuándo, por quién y por qué.</summary>
    private static async Task<IResult> MyCorrections(HttpContext ctx, PmgmDbContext db, CancellationToken ct)
    {
        ctx.Response.Headers.CacheControl = "private, no-store";
        var sub = Subject(ctx);
        if (string.IsNullOrWhiteSpace(sub)) return Results.Ok(new { items = Array.Empty<object>() });
        var memberId = await db.Database.SqlQuery<Guid>(
            $"SELECT \"MemberId\" AS \"Value\" FROM core.member_identity_links WHERE \"Subject\" = {sub} AND \"RevokedAtUtc\" IS NULL LIMIT 1").FirstOrDefaultAsync(ct);
        if (memberId == Guid.Empty) return Results.Ok(new { items = Array.Empty<object>() });
        var rows = await db.SupportCorrections.AsNoTracking().Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(50).ToListAsync(ct);
        return Results.Ok(new
        {
            items = rows.Select(x => new { id = x.Id, field = x.Field, label = SupportCorrectionPolicy.Label(x.Field), detail = x.Detail, operatorName = x.OperatorName, reason = x.Reason, createdAtUtc = x.CreatedAtUtc })
        });
    }
}
