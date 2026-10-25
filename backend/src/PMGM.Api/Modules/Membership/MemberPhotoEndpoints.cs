using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Membership;

/// <summary>
/// Foto de perfil (#609). Solo el titular la cambia, retira o decide su visibilidad. La ven los Hermanos
/// autenticados únicamente si el titular la hizo visible; nunca de forma pública ni anónima.
/// La ruta de lectura de terceros queda fuera de /api/members* para no depender de perfiles dinámicos de esa vista.
/// </summary>
public static class MemberPhotoEndpoints
{
    public static IEndpointRouteBuilder MapMemberPhotoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var own = endpoints.MapGroup("/api/member-self/photo").WithTags("Member Photo").RequireAuthorization();
        own.MapGet("", GetOwnStatusAsync);
        own.MapGet("/content", GetOwnContentAsync);
        own.MapPut("", UploadOwnAsync);
        own.MapDelete("", DeleteOwnAsync);
        own.MapPut("/visibility", SetVisibilityAsync);
        endpoints.MapGet("/api/member-photos/{memberId:guid}", GetForMemberAsync).WithTags("Member Photo").RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetOwnStatusAsync(HttpContext http, PmgmDbContext db, IInstitutionalMemberContextResolver resolver, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var context = await resolver.ResolveAsync(http.User, ct);
        if (context is null) return Results.NotFound(new { message = "La identidad autenticada no está vinculada a un Hermano institucional activo." });
        var photo = await db.MemberPhotos.AsNoTracking().Where(x => x.MemberId == context.MemberId)
            .Select(x => new { x.VisibleToMembers, x.UpdatedAtUtc, x.Width }).SingleOrDefaultAsync(ct);
        return Results.Ok(new { hasPhoto = photo is not null, visibleToMembers = photo?.VisibleToMembers ?? false, updatedAtUtc = photo?.UpdatedAtUtc });
    }

    private static async Task<IResult> GetOwnContentAsync(HttpContext http, PmgmDbContext db, IInstitutionalMemberContextResolver resolver, CancellationToken ct)
    {
        var context = await resolver.ResolveAsync(http.User, ct);
        if (context is null) return Results.NotFound();
        return await ServeAsync(http, db, context.MemberId, ct);
    }

    private static async Task<IResult> GetForMemberAsync(Guid memberId, HttpContext http, PmgmDbContext db, IInstitutionalMemberContextResolver resolver, CancellationToken ct)
    {
        var viewer = await resolver.ResolveAsync(http.User, ct);
        if (viewer is null) return Results.NotFound(); // solo Hermanos autenticados
        var isOwner = viewer.MemberId == memberId;
        if (!isOwner && !await db.MemberPhotos.AnyAsync(x => x.MemberId == memberId && x.VisibleToMembers, ct)) return Results.NotFound();
        return await ServeAsync(http, db, memberId, ct);
    }

    private static async Task<IResult> ServeAsync(HttpContext http, PmgmDbContext db, Guid memberId, CancellationToken ct)
    {
        var photo = await db.MemberPhotos.AsNoTracking().Where(x => x.MemberId == memberId).SingleOrDefaultAsync(ct);
        if (photo is null) return Results.NotFound();
        // Sin caché compartida; ETag para revalidar rápido si el titular la cambia o la oculta.
        http.Response.Headers.CacheControl = "private, no-cache";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.File(photo.Content, photo.ContentType, entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{photo.Sha256}\""));
    }

    private static async Task<IResult> UploadOwnAsync(HttpContext http, PmgmDbContext db, IInstitutionalMemberContextResolver resolver, IAuditService audit, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var context = await resolver.ResolveAsync(http.User, ct);
        if (context is null) return Results.NotFound(new { message = "La identidad autenticada no está vinculada a un Hermano institucional activo." });
        if (http.Request.ContentLength is > MemberPhotoPolicy.MaxBytes) return Results.BadRequest(new { message = "La foto supera el tamaño máximo (400 KB). Use una imagen más liviana." });
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MemberPhotoPolicy.MaxBytes) return Results.BadRequest(new { message = "La foto supera el tamaño máximo (400 KB). Use una imagen más liviana." });
        }
        var normalized = MemberPhotoPolicy.Normalize(buffer.ToArray());
        if (normalized.Error is not null) return Results.BadRequest(new { message = normalized.Error });

        var photo = await db.MemberPhotos.SingleOrDefaultAsync(x => x.MemberId == context.MemberId, ct);
        var isNew = photo is null;
        photo ??= db.MemberPhotos.Add(new MemberPhoto { MemberId = context.MemberId }).Entity;
        photo.Content = normalized.Content!;
        photo.ContentType = normalized.ContentType!;
        photo.Width = normalized.Width;
        photo.Height = normalized.Height;
        photo.Sha256 = Convert.ToHexString(SHA256.HashData(photo.Content)).ToLowerInvariant();
        photo.UpdatedAtUtc = DateTimeOffset.UtcNow;
        // Una foto nueva nace NO visible: el Hermano decide expresamente si los demás la ven.
        if (isNew) photo.VisibleToMembers = false;
        audit.Add(http, isNew ? "member.photo.added" : "member.photo.replaced", "Member", context.MemberId.ToString(), null,
            AuditResults.Success, new { bytes = photo.Content.Length, photo.Width, photo.ContentType });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { hasPhoto = true, visibleToMembers = photo.VisibleToMembers, updatedAtUtc = photo.UpdatedAtUtc });
    }

    private static async Task<IResult> DeleteOwnAsync(HttpContext http, PmgmDbContext db, IInstitutionalMemberContextResolver resolver, IAuditService audit, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var context = await resolver.ResolveAsync(http.User, ct);
        if (context is null) return Results.NotFound();
        var photo = await db.MemberPhotos.SingleOrDefaultAsync(x => x.MemberId == context.MemberId, ct);
        if (photo is null) return Results.NoContent();
        db.MemberPhotos.Remove(photo);
        audit.Add(http, "member.photo.removed", "Member", context.MemberId.ToString(), null, AuditResults.Success);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetVisibilityAsync(SetPhotoVisibilityRequest request, HttpContext http, PmgmDbContext db, IInstitutionalMemberContextResolver resolver, IAuditService audit, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var context = await resolver.ResolveAsync(http.User, ct);
        if (context is null) return Results.NotFound();
        var photo = await db.MemberPhotos.SingleOrDefaultAsync(x => x.MemberId == context.MemberId, ct);
        if (photo is null) return Results.Conflict(new { message = "Primero suba una foto." });
        if (photo.VisibleToMembers != request.Visible)
        {
            photo.VisibleToMembers = request.Visible;
            photo.UpdatedAtUtc = DateTimeOffset.UtcNow;
            audit.Add(http, "member.photo.visibility_changed", "Member", context.MemberId.ToString(), null, AuditResults.Success, new { visible = request.Visible });
            await db.SaveChangesAsync(ct);
        }
        return Results.Ok(new { hasPhoto = true, visibleToMembers = photo.VisibleToMembers, updatedAtUtc = photo.UpdatedAtUtc });
    }
}

public sealed record SetPhotoVisibilityRequest(bool Visible);
