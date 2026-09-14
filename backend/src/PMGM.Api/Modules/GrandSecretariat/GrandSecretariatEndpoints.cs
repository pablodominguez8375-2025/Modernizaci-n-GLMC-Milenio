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

public static class GrandSecretariatEndpoints
{
    public static IEndpointRouteBuilder MapGrandSecretariatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gran-secretaria")
            .WithTags("Gran Secretaría")
            .RequireAuthorization();

        group.MapPost("/espacios", CreateSpaceAsync);
        group.MapGet("/espacios/disponibilidad", GetAvailabilityAsync);
        group.MapPost("/reservas", CreateReservationAsync);
        group.MapPost("/reservas/{reservationId:guid}/cancelar", CancelReservationAsync);
        group.MapPost("/documentos", IssueInstitutionalDocumentAsync);
        group.MapGet("/documentos", GetDocumentsAsync);
        group.MapPost("/ceremonias/{requestId:guid}/autorizacion", IssueCeremonyAuthorizationAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateSpaceAsync(
        CreateInstitutionalSpaceRequest request,
        HttpContext httpContext,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManage(access, httpContext.User))
        {
            return Results.Forbid();
        }

        if (!IsSpaceTypeValid(request.SpaceType) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { message = "Código, nombre y tipo de espacio son obligatorios y deben ser válidos." });
        }

        if (request.Capacity is <= 0)
        {
            return Results.BadRequest(new { message = "La capacidad debe ser mayor que cero." });
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.InstitutionalSpaces.AnyAsync(x => x.Code == code, cancellationToken))
        {
            return Results.Conflict(new { message = "Ya existe un espacio institucional con ese código." });
        }

        var space = new InstitutionalSpace
        {
            Code = code,
            Name = request.Name.Trim(),
            SpaceType = request.SpaceType,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Capacity = request.Capacity,
            Status = GrandSecretariatCodes.SpaceStatus.Active
        };

        db.InstitutionalSpaces.Add(space);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "grand_secretariat.space.created",
            nameof(InstitutionalSpace),
            space.Id.ToString(),
            null,
            AuditResults.Success,
            new { space.Code, space.SpaceType, space.Status }));

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gran-secretaria/espacios/{space.Id}", new
        {
            space.Id,
            space.Code,
            space.Name,
            space.SpaceType,
            space.Location,
            space.Capacity,
            space.Status
        });
    }

    private static async Task<IResult> GetAvailabilityAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        HttpContext httpContext,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManage(access, httpContext.User))
        {
            return Results.Forbid();
        }

        if (toUtc <= fromUtc)
        {
            return Results.BadRequest(new { message = "El término del período debe ser posterior al inicio." });
        }

        var spaces = await db.InstitutionalSpaces
            .AsNoTracking()
            .Where(x => x.Status == GrandSecretariatCodes.SpaceStatus.Active)
            .OrderBy(x => x.SpaceType)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var busySpaceIds = await db.SpaceReservations
            .AsNoTracking()
            .Where(x =>
                x.Status == GrandSecretariatCodes.ReservationStatus.Reserved &&
                x.StartsAtUtc < toUtc &&
                x.EndsAtUtc > fromUtc)
            .Select(x => x.SpaceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var busy = busySpaceIds.ToHashSet();
        return Results.Ok(new
        {
            fromUtc,
            toUtc,
            total = spaces.Count,
            available = spaces.Count(x => !busy.Contains(x.Id)),
            items = spaces.Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.SpaceType,
                x.Location,
                x.Capacity,
                isAvailable = !busy.Contains(x.Id)
            })
        });
    }

    private static async Task<IResult> CreateReservationAsync(
        CreateSpaceReservationRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManage(access, httpContext.User))
        {
            return Results.Forbid();
        }

        if (request.EndsAtUtc <= request.StartsAtUtc || string.IsNullOrWhiteSpace(request.Purpose))
        {
            return Results.BadRequest(new { message = "La reserva requiere un propósito y un período de tiempo válido." });
        }

        var organizationExists = await institutionalDb.Organizations
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.OrganizationId, cancellationToken);
        if (!organizationExists)
        {
            return Results.NotFound(new { message = "El Taller u organización indicado no existe." });
        }

        if (request.CeremonyRequestId is not null)
        {
            var ceremony = await institutionalDb.CeremonyRequests
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.CeremonyRequestId.Value, cancellationToken);

            if (ceremony is null)
            {
                return Results.NotFound(new { message = "La solicitud de ceremonia indicada no existe." });
            }

            if (ceremony.OrganizationId != request.OrganizationId)
            {
                return Results.BadRequest(new { message = "La ceremonia no corresponde al Taller indicado." });
            }

            if (ceremony.Status != CeremonyCodes.RequestStatus.Authorized)
            {
                return Results.Conflict(new { message = "Una reserva vinculada a ceremonia requiere que la ceremonia esté previamente autorizada." });
            }
        }

        var space = await db.InstitutionalSpaces
            .SingleOrDefaultAsync(x => x.Id == request.SpaceId, cancellationToken);
        if (space is null)
        {
            return Results.NotFound(new { message = "El templo o sala indicado no existe." });
        }

        if (space.Status != GrandSecretariatCodes.SpaceStatus.Active)
        {
            return Results.Conflict(new { message = "El espacio institucional no está disponible para reservas." });
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var hasConflict = await db.SpaceReservations.AnyAsync(
                x => x.SpaceId == request.SpaceId &&
                     x.Status == GrandSecretariatCodes.ReservationStatus.Reserved &&
                     x.StartsAtUtc < request.EndsAtUtc &&
                     x.EndsAtUtc > request.StartsAtUtc,
                cancellationToken);

            if (hasConflict)
            {
                db.AuditEvents.Add(AuditEventFactory.Create(
                    httpContext,
                    "grand_secretariat.space_reservation.rejected",
                    nameof(InstitutionalSpaceReservation),
                    Guid.Empty.ToString(),
                    request.OrganizationId,
                    AuditResults.Rejected,
                    new { request.SpaceId, reason = "schedule_conflict" }));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Results.Conflict(new { message = "El templo o sala ya está reservado total o parcialmente en ese horario." });
            }

            var reservation = new InstitutionalSpaceReservation
            {
                SpaceId = request.SpaceId,
                OrganizationId = request.OrganizationId,
                CeremonyRequestId = request.CeremonyRequestId,
                Purpose = request.Purpose.Trim(),
                StartsAtUtc = request.StartsAtUtc,
                EndsAtUtc = request.EndsAtUtc,
                Status = GrandSecretariatCodes.ReservationStatus.Reserved,
                Notes = request.Notes
            };

            db.SpaceReservations.Add(reservation);
            db.AuditEvents.Add(AuditEventFactory.Create(
                httpContext,
                "grand_secretariat.space_reservation.created",
                nameof(InstitutionalSpaceReservation),
                reservation.Id.ToString(),
                reservation.OrganizationId,
                AuditResults.Success,
                new
                {
                    reservation.SpaceId,
                    reservation.CeremonyRequestId,
                    reservation.StartsAtUtc,
                    reservation.EndsAtUtc,
                    reservation.Status
                }));

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Created($"/api/gran-secretaria/reservas/{reservation.Id}", new
            {
                reservation.Id,
                reservation.SpaceId,
                reservation.OrganizationId,
                reservation.CeremonyRequestId,
                reservation.Purpose,
                reservation.StartsAtUtc,
                reservation.EndsAtUtc,
                reservation.Status
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return Results.Conflict(new { message = "El horario fue tomado por otra reserva concurrente. Revise disponibilidad e intente nuevamente." });
        }
    }

    private static async Task<IResult> CancelReservationAsync(
        Guid reservationId,
        HttpContext httpContext,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManage(access, httpContext.User))
        {
            return Results.Forbid();
        }

        var reservation = await db.SpaceReservations.SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation is null)
        {
            return Results.NotFound();
        }

        if (reservation.Status == GrandSecretariatCodes.ReservationStatus.Cancelled)
        {
            return Results.Conflict(new { message = "La reserva ya está cancelada." });
        }

        reservation.Status = GrandSecretariatCodes.ReservationStatus.Cancelled;
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "grand_secretariat.space_reservation.cancelled",
            nameof(InstitutionalSpaceReservation),
            reservation.Id.ToString(),
            reservation.OrganizationId,
            AuditResults.Success,
            new { reservation.SpaceId, reservation.Status }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { reservation.Id, reservation.Status });
    }

    private static async Task<IResult> IssueInstitutionalDocumentAsync(
        IssueSecretariatDocumentRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManage(access, httpContext.User))
        {
            return Results.Forbid();
        }

        if (request.DocumentType is not GrandSecretariatCodes.DocumentType.Decree and not GrandSecretariatCodes.DocumentType.Communication ||
            string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest(new { message = "El tipo, título y contenido del documento son obligatorios." });
        }

        if (request.OrganizationId is not null)
        {
            var organizationExists = await institutionalDb.Organizations
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.OrganizationId.Value, cancellationToken);
            if (!organizationExists)
            {
                return Results.NotFound(new { message = "La organización indicada no existe." });
            }
        }

        var document = new SecretariatDocument
        {
            DocumentType = request.DocumentType,
            DocumentCode = NewDocumentCode(request.DocumentType),
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            OrganizationId = request.OrganizationId,
            Status = GrandSecretariatCodes.DocumentStatus.Issued,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            IssuedBySubject = GetSubject(httpContext.User)
        };

        db.SecretariatDocuments.Add(document);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "grand_secretariat.document.issued",
            nameof(SecretariatDocument),
            document.Id.ToString(),
            document.OrganizationId,
            AuditResults.Success,
            new { document.DocumentType, document.DocumentCode, document.Status }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gran-secretaria/documentos/{document.Id}", ToDocumentDto(document));
    }

    private static async Task<IResult> GetDocumentsAsync(
        string? documentType,
        HttpContext httpContext,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManage(access, httpContext.User))
        {
            return Results.Forbid();
        }

        var query = db.SecretariatDocuments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(documentType))
        {
            query = query.Where(x => x.DocumentType == documentType);
        }

        var documents = await query
            .OrderByDescending(x => x.IssuedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        return Results.Ok(new { total = documents.Count, items = documents.Select(ToDocumentDto) });
    }

    private static async Task<IResult> IssueCeremonyAuthorizationAsync(
        Guid requestId,
        IssueCeremonyAuthorizationRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        GrandSecretariatDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManage(access, httpContext.User))
        {
            return Results.Forbid();
        }

        var ceremony = await institutionalDb.CeremonyRequests
            .AsNoTracking()
            .Include(x => x.Organization)
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null)
        {
            return Results.NotFound();
        }

        if (ceremony.Status != CeremonyCodes.RequestStatus.Authorized)
        {
            return Results.Conflict(new { message = "Gran Secretaría sólo puede emitir la autorización formal de una ceremonia ya autorizada por el flujo institucional." });
        }

        var alreadyIssued = await db.SecretariatDocuments.AnyAsync(
            x => x.RelatedCeremonyRequestId == requestId &&
                 x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorization &&
                 x.Status == GrandSecretariatCodes.DocumentStatus.Issued,
            cancellationToken);
        if (alreadyIssued)
        {
            return Results.Conflict(new { message = "La ceremonia ya cuenta con una autorización formal vigente." });
        }

        InstitutionalSpaceReservation? reservation = null;
        if (request.SpaceReservationId is not null)
        {
            reservation = await db.SpaceReservations
                .AsNoTracking()
                .Include(x => x.Space)
                .SingleOrDefaultAsync(x => x.Id == request.SpaceReservationId.Value, cancellationToken);

            if (reservation is null)
            {
                return Results.NotFound(new { message = "La reserva de templo o sala indicada no existe." });
            }

            if (reservation.Status != GrandSecretariatCodes.ReservationStatus.Reserved ||
                reservation.OrganizationId != ceremony.OrganizationId ||
                reservation.CeremonyRequestId != ceremony.Id)
            {
                return Results.BadRequest(new { message = "La reserva indicada no corresponde a esta ceremonia autorizada." });
            }
        }

        var dateText = ceremony.ProposedDate?.ToString("dd-MM-yyyy") ?? "fecha por confirmar";
        var spaceText = reservation is null
            ? "sin asignación de templo o sala en este documento"
            : $"en {reservation.Space.Name}, desde {reservation.StartsAtUtc:dd-MM-yyyy HH:mm} UTC hasta {reservation.EndsAtUtc:dd-MM-yyyy HH:mm} UTC";

        var document = new SecretariatDocument
        {
            DocumentType = GrandSecretariatCodes.DocumentType.CeremonyAuthorization,
            DocumentCode = NewDocumentCode(GrandSecretariatCodes.DocumentType.CeremonyAuthorization),
            Title = $"Autorización de ceremonia — {ceremony.CeremonyType}",
            Content = $"Gran Secretaría deja constancia de la autorización institucional de la ceremonia '{ceremony.CeremonyType}' solicitada por {ceremony.Organization.Name}, para {dateText}, {spaceText}. La presente autorización se emite una vez cumplidas las validaciones institucionales exigibles.",
            OrganizationId = ceremony.OrganizationId,
            RelatedCeremonyRequestId = ceremony.Id,
            SpaceReservationId = reservation?.Id,
            Status = GrandSecretariatCodes.DocumentStatus.Issued,
            IssuedAtUtc = DateTimeOffset.UtcNow,
            IssuedBySubject = GetSubject(httpContext.User)
        };

        db.SecretariatDocuments.Add(document);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "grand_secretariat.ceremony_authorization.issued",
            nameof(SecretariatDocument),
            document.Id.ToString(),
            ceremony.OrganizationId,
            AuditResults.Success,
            new
            {
                document.DocumentCode,
                document.DocumentType,
                document.RelatedCeremonyRequestId,
                document.SpaceReservationId,
                document.Status
            }));

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gran-secretaria/documentos/{document.Id}", ToDocumentDto(document));
    }

    private static bool CanManage(IInstitutionalAccessService access, ClaimsPrincipal user)
        => access.HasOrderScope(user) &&
           access.HasRole(user, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranSecretaria);

    private static bool IsSpaceTypeValid(string value)
        => value is GrandSecretariatCodes.SpaceType.Temple or GrandSecretariatCodes.SpaceType.SecretariatRoom;

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub")
           ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? "unknown";

    private static string NewDocumentCode(string type)
    {
        var prefix = type switch
        {
            GrandSecretariatCodes.DocumentType.Decree => "DEC",
            GrandSecretariatCodes.DocumentType.Communication => "COM",
            GrandSecretariatCodes.DocumentType.CeremonyAuthorization => "AUT-CER",
            _ => "DOC"
        };

        return $"{prefix}-{DateTimeOffset.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    private static object ToDocumentDto(SecretariatDocument document)
        => new
        {
            document.Id,
            document.DocumentType,
            document.DocumentCode,
            document.Title,
            document.Content,
            document.OrganizationId,
            document.RelatedCeremonyRequestId,
            document.SpaceReservationId,
            document.Status,
            document.IssuedAtUtc,
            document.IssuedBySubject
        };
}

public sealed record CreateInstitutionalSpaceRequest(
    string Code,
    string Name,
    string SpaceType,
    string? Location,
    int? Capacity);

public sealed record CreateSpaceReservationRequest(
    Guid SpaceId,
    Guid OrganizationId,
    Guid? CeremonyRequestId,
    string Purpose,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Notes);

public sealed record IssueSecretariatDocumentRequest(
    string DocumentType,
    string Title,
    string Content,
    Guid? OrganizationId);

public sealed record IssueCeremonyAuthorizationRequest(Guid? SpaceReservationId);
