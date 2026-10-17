using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.GrandSecretariat.Entities;

namespace PMGM.Api.Modules.GrandSecretariat;

/// <summary>Issue #531: Taller solicita; solo Gran Secretaría puede confirmar una reserva.</summary>
public static class SpaceRequestEndpoints
{
    public static IEndpointRouteBuilder MapSpaceRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var lodge = app.MapGroup("/api/secretaria").WithTags("Secretaría: espacios").RequireAuthorization();
        lodge.MapPost("/solicitudes-espacio", CreateAsync);
        lodge.MapGet("/solicitudes-espacio", ListMineAsync);
        lodge.MapPost("/solicitudes-espacio/{id:guid}/cancelar", CancelAsync);
        lodge.MapGet("/espacios/disponibilidad", AvailabilityAsync);

        var grand = app.MapGroup("/api/gran-secretaria").WithTags("Gran Secretaría: solicitudes").RequireAuthorization();
        grand.MapGet("/solicitudes-espacio", ListAllAsync);
        grand.MapPost("/solicitudes-espacio/{id:guid}/aprobar", ApproveAsync);
        grand.MapPost("/solicitudes-espacio/{id:guid}/rechazar", RejectAsync);
        return app;
    }

    // Se exige un Taller inequívoco en la sesión; no se acepta un OrganizationId enviado por el cliente.
    // El rol institucional del mismo Taller, no un permiso heredado de superadministrador, es obligatorio.
    internal static bool TryLodge(ClaimsPrincipal user, IInstitutionalAccessService access, out Guid organizationId)
    {
        organizationId = Guid.Empty;
        if (!user.Claims.Any(x => x.Type == InstitutionalClaims.Role &&
             x.Value.Equals(InstitutionalRoles.TallerSecretaria, StringComparison.OrdinalIgnoreCase)))
            return false;
        var orgs = user.Claims.Where(x => x.Type == InstitutionalClaims.Organization)
            .Select(x => Guid.TryParse(x.Value, out var parsed) ? parsed : Guid.Empty)
            .Distinct().ToArray();
        if (orgs.Length != 1 || orgs[0] == Guid.Empty) return false;
        organizationId = orgs[0];
        return access.CanManageLodgeSecretariat(user, organizationId);
    }

    internal static string? Validate(CreateWorkshopSpaceRequest body)
    {
        if (body.StartsAtUtc.Offset != TimeSpan.Zero || body.EndsAtUtc.Offset != TimeSpan.Zero ||
            body.EndsAtUtc <= body.StartsAtUtc)
            return "El intervalo debe ser UTC y finalizar después de su inicio.";
        if (body.StartsAtUtc <= DateTimeOffset.UtcNow)
            return "La solicitud debe tener inicio futuro.";
        if (body.SpaceType is not (GrandSecretariatCodes.SpaceType.Temple or GrandSecretariatCodes.SpaceType.SecretariatRoom))
            return "Tipo de espacio no permitido.";
        if (body.Purpose is not ("tenida" or "docencia" or "ceremonia" or "otro"))
            return "Tipo de actividad no permitido.";
        if (body.CeremonyRequestId is not null && body.Purpose != "ceremonia")
            return "Una ceremonia solo puede vincularse a una solicitud de tipo ceremonia.";
        if (body.Notes?.Length > 500 || body.EquipmentRequest?.Length > 500 ||
            body.EquipmentResponsible?.Length > 240 || body.VmApprovalReference?.Length > 240 ||
            body.ExpectedAttendees is <= 0)
            return "Longitud o aforo inválido.";
        return null;
    }

    private static string Subject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static object Dto(InstitutionalSpaceRequest x) => new
    {
        x.Id, x.OrganizationId, x.PreferredSpaceType, x.PreferredSpaceId,
        x.StartsAtUtc, x.EndsAtUtc, x.Purpose, x.CeremonyRequestId, x.Notes,
        x.ExpectedAttendees, x.EquipmentRequest, x.EquipmentResponsible, x.VmApprovalReference,
        x.Status, x.ResponseReason, x.SpaceReservationId, x.CreatedAtUtc, x.DecidedAtUtc
    };

    private static async Task<IResult> CreateAsync(
        CreateWorkshopSpaceRequest body, HttpContext http, PmgmDbContext institutions,
        GrandSecretariatDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!TryLodge(http.User, access, out var lodgeId)) return Results.Forbid();
        var error = Validate(body);
        if (error is not null) return Results.BadRequest(new { message = error });

        if (!await institutions.Organizations.AsNoTracking()
            .AnyAsync(x => x.Id == lodgeId && x.Type == "workshop", ct))
            return Results.Forbid();

        if (body.PreferredSpaceId is not null)
        {
            var space = await db.InstitutionalSpaces.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == body.PreferredSpaceId.Value, ct);
            if (space is null || space.Status != GrandSecretariatCodes.SpaceStatus.Active ||
                space.SpaceType != body.SpaceType)
                return Results.BadRequest(new { message = "El espacio solicitado no está habilitado para ese tipo." });
        }
        if (body.CeremonyRequestId is not null &&
            !await institutions.CeremonyRequests.AsNoTracking()
                .AnyAsync(x => x.Id == body.CeremonyRequestId && x.OrganizationId == lodgeId, ct))
            return Results.BadRequest(new { message = "La ceremonia no corresponde al Taller." });

        var row = new InstitutionalSpaceRequest
        {
            OrganizationId = lodgeId,
            PreferredSpaceType = body.SpaceType,
            PreferredSpaceId = body.PreferredSpaceId,
            CeremonyRequestId = body.CeremonyRequestId,
            StartsAtUtc = body.StartsAtUtc,
            EndsAtUtc = body.EndsAtUtc,
            Purpose = body.Purpose,
            Notes = body.Notes?.Trim(),
            ExpectedAttendees = body.ExpectedAttendees,
            EquipmentRequest = body.EquipmentRequest?.Trim(),
            EquipmentResponsible = body.EquipmentResponsible?.Trim(),
            VmApprovalReference = body.VmApprovalReference?.Trim(),
            Status = "pending", CreatedBySubject = Subject(http.User)
        };
        db.SpaceRequests.Add(row);
        db.AuditEvents.Add(AuditEventFactory.Create(http, "grand_secretariat.space_request.created",
            nameof(InstitutionalSpaceRequest), row.Id.ToString(), lodgeId, AuditResults.Success,
            new { row.PreferredSpaceType, row.PreferredSpaceId, row.Purpose, row.StartsAtUtc, row.EndsAtUtc }));
        await db.SaveChangesAsync(ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Created("/api/secretaria/solicitudes-espacio/" + row.Id, Dto(row));
    }

    private static async Task<IResult> ListMineAsync(
        HttpContext http, GrandSecretariatDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!TryLodge(http.User, access, out var lodgeId)) return Results.Forbid();
        var items = await db.SpaceRequests.AsNoTracking()
            .Where(x => x.OrganizationId == lodgeId).OrderByDescending(x => x.CreatedAtUtc)
            .Take(300).ToListAsync(ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items = items.Select(Dto) });
    }

    private static async Task<IResult> AvailabilityAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtc, HttpContext http,
        GrandSecretariatDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!TryLodge(http.User, access, out _)) return Results.Forbid();
        if (toUtc <= fromUtc || toUtc - fromUtc > TimeSpan.FromDays(31))
            return Results.BadRequest(new { message = "Período de consulta inválido o superior a 31 días." });
        var spaces = await db.InstitutionalSpaces.AsNoTracking()
            .Where(x => x.Status == GrandSecretariatCodes.SpaceStatus.Active)
            .OrderBy(x => x.Name).ToListAsync(ct);
        var occupied = (await db.SpaceReservations.AsNoTracking()
            .Where(x => x.Status == GrandSecretariatCodes.ReservationStatus.Reserved &&
                x.StartsAtUtc < toUtc && x.EndsAtUtc > fromUtc)
            .Select(x => x.SpaceId).Distinct().ToListAsync(ct)).ToHashSet();
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { items = spaces.Select(x => new {
            x.Id, x.Name, x.SpaceType, isAvailable = !occupied.Contains(x.Id) }) });
    }

    private static async Task<IResult> ListAllAsync(
        string? status, Guid? organizationId, HttpContext http,
        GrandSecretariatDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageGrandSecretariat(http.User)) return Results.Forbid();
        if (status is not null && status is not ("pending" or "approved" or "rejected" or "cancelled"))
            return Results.BadRequest(new { message = "Estado inválido." });
        var query = db.SpaceRequests.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(x => x.Status == status);
        if (organizationId is not null) query = query.Where(x => x.OrganizationId == organizationId);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Take(500).ToListAsync(ct);
        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items = items.Select(Dto) });
    }

    private static async Task<IResult> CancelAsync(
        Guid id, HttpContext http, GrandSecretariatDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!TryLodge(http.User, access, out var lodgeId)) return Results.Forbid();
        return await DecideAsync(id, "cancelled", null, lodgeId, http, db, ct);
    }

    private static async Task<IResult> RejectAsync(
        Guid id, RejectSpaceRequest body, HttpContext http, GrandSecretariatDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageGrandSecretariat(http.User)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(body.Reason) || body.Reason.Length > 500)
            return Results.BadRequest(new { message = "Motivo de rechazo obligatorio, máximo 500 caracteres." });
        return await DecideAsync(id, "rejected", body.Reason.Trim(), null, http, db, ct);
    }

    private static async Task<IResult> DecideAsync(
        Guid id, string status, string? reason, Guid? lodgeId, HttpContext http,
        GrandSecretariatDbContext db, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var item = await db.SpaceRequests.SingleOrDefaultAsync(x => x.Id == id &&
                (lodgeId == null || x.OrganizationId == lodgeId), ct);
            if (item is null) return Results.NotFound();
            if (item.Status != "pending")
                return Results.Conflict(new { message = "La solicitud ya fue resuelta." });
            item.Status = status;
            item.ResponseReason = reason;
            item.DecidedBySubject = Subject(http.User);
            item.DecidedAtUtc = DateTimeOffset.UtcNow;
            db.AuditEvents.Add(AuditEventFactory.Create(http, "grand_secretariat.space_request." + status,
                nameof(InstitutionalSpaceRequest), item.Id.ToString(), item.OrganizationId,
                AuditResults.Success, new { item.Status, item.DecidedAtUtc }));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(Dto(item));
        }
        catch (PostgresException ex) when (ex.SqlState is "40001" or "40P01")
        {
            return Results.Conflict(new { message = "Otra sesión modificó la solicitud; recargue y reintente." });
        }
    }

    private static async Task<IResult> ApproveAsync(
        Guid id, ApproveSpaceRequest body, HttpContext http, PmgmDbContext institutions,
        GrandSecretariatDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageGrandSecretariat(http.User)) return Results.Forbid();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var item = await db.SpaceRequests.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound();
            if (item.Status != "pending")
                return Results.Conflict(new { message = "La solicitud ya fue resuelta." });

            var space = await db.InstitutionalSpaces.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == body.SpaceId, ct);
            if (space is null) return Results.NotFound(new { message = "El espacio no existe." });
            if (space.Status != GrandSecretariatCodes.SpaceStatus.Active ||
                space.SpaceType != item.PreferredSpaceType)
                return Results.Conflict(new { message = "El espacio no está habilitado para este tipo de solicitud." });
            if (item.ExpectedAttendees is not null && space.Capacity is not null &&
                item.ExpectedAttendees > space.Capacity)
                return Results.Conflict(new { message = "El espacio no tiene capacidad suficiente." });

            if (item.CeremonyRequestId is not null)
            {
                var authorized = await institutions.CeremonyRequests.AsNoTracking()
                    .AnyAsync(x => x.Id == item.CeremonyRequestId &&
                        x.OrganizationId == item.OrganizationId &&
                        x.Status == CeremonyCodes.RequestStatus.Authorized, ct);
                var plancha = await db.SecretariatDocuments.AsNoTracking().AnyAsync(x =>
                    x.RelatedCeremonyRequestId == item.CeremonyRequestId &&
                    x.DocumentType == GrandSecretariatCodes.DocumentType.Plancha &&
                    x.PlanchaKind == GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization &&
                    x.Status == GrandSecretariatCodes.DocumentStatus.Issued, ct);
                if (!authorized || !plancha)
                    return Results.Conflict(new { message = "La reserva ceremonial requiere autorización y Plancha emitida previamente." });
            }

            var overlap = await db.SpaceReservations.AnyAsync(x =>
                x.SpaceId == body.SpaceId &&
                x.Status == GrandSecretariatCodes.ReservationStatus.Reserved &&
                x.StartsAtUtc < item.EndsAtUtc && x.EndsAtUtc > item.StartsAtUtc, ct);
            if (overlap)
                return Results.Conflict(new { message = "El horario está ocupado; la solicitud permanece pendiente." });

            var reservation = new InstitutionalSpaceReservation
            {
                SpaceId = body.SpaceId,
                OrganizationId = item.OrganizationId,
                CeremonyRequestId = item.CeremonyRequestId,
                Purpose = item.Purpose,
                StartsAtUtc = item.StartsAtUtc, EndsAtUtc = item.EndsAtUtc,
                Status = GrandSecretariatCodes.ReservationStatus.Reserved,
                Notes = item.Notes
            };
            db.SpaceReservations.Add(reservation);
            item.SpaceReservationId = reservation.Id;
            item.Status = "approved";
            item.DecidedAtUtc = DateTimeOffset.UtcNow;
            item.DecidedBySubject = Subject(http.User);
            db.AuditEvents.Add(AuditEventFactory.Create(http,
                "grand_secretariat.space_request.approved", nameof(InstitutionalSpaceRequest),
                item.Id.ToString(), item.OrganizationId, AuditResults.Success,
                new { item.SpaceReservationId, item.DecidedAtUtc }));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            http.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(Dto(item));
        }
        catch (Exception ex) when (ex is PostgresException { SqlState: "40001" or "40P01" or "23505" } ||
            ex is DbUpdateException)
        {
            return Results.Conflict(new { message = "Otra reserva cambió la disponibilidad; actualice y reintente." });
        }
    }
}

public sealed record CreateWorkshopSpaceRequest(
    string SpaceType, Guid? PreferredSpaceId, DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc, string Purpose, Guid? CeremonyRequestId,
    string? Notes, int? ExpectedAttendees, string? EquipmentRequest,
    string? EquipmentResponsible, string? VmApprovalReference);

public sealed record RejectSpaceRequest(string Reason);
public sealed record ApproveSpaceRequest(Guid SpaceId);
