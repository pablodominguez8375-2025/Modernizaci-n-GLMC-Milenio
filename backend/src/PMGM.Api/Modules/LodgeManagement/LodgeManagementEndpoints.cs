using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.LodgeManagement;

public static class LodgeManagementEndpoints
{
    public static IEndpointRouteBuilder MapLodgeManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial")
            .WithTags("Gestión Logial")
            .RequireAuthorization();

        group.MapGet("/talleres/{organizationId:guid}/miembros/opciones", GetMemberOptionsAsync);
        group.MapPost("/talleres/{organizationId:guid}/tenidas", CreateMeetingAsync);
        group.MapGet("/talleres/{organizationId:guid}/tenidas", GetMeetingsAsync);
        group.MapPost("/tenidas/{meetingId:guid}/cerrar", CloseMeetingAsync);
        group.MapPost("/tenidas/{meetingId:guid}/asistencia", RecordAttendanceAsync);
        group.MapGet("/tenidas/{meetingId:guid}/asistencia", GetCurrentAttendanceAsync);
        group.MapPost("/tenidas/{meetingId:guid}/votaciones", RecordAnonymousBallotAsync);
        group.MapGet("/tenidas/{meetingId:guid}/votaciones", GetAnonymousBallotsAsync);
        group.MapGet("/tenidas/{meetingId:guid}/extracto-acta", GenerateMinuteExtractAsync);
        group.MapPost("/tenidas/{meetingId:guid}/actas", CreateMinuteVersionAsync);
        group.MapGet("/tenidas/{meetingId:guid}/actas", GetMinutesAsync);
        group.MapPost("/tenidas/{meetingId:guid}/actas/{minuteId:guid}/aprobar", ApproveMinuteAsync);

        return endpoints;
    }

    private static async Task<IResult> GetMemberOptionsAsync(
        Guid organizationId,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();

        var items = await institutionalDb.Memberships
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId &&
                        x.EndDate == null &&
                        x.Status == MembershipCodes.MembershipStatus.Active)
            .OrderBy(x => x.Member.Person.LastNames)
            .ThenBy(x => x.Member.Person.FirstNames)
            .Select(x => new LodgeMemberOptionDto(
                x.MemberId,
                (x.Member.Person.FirstNames + " " + x.Member.Person.LastNames).Trim()))
            .Take(1000)
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeMemberOptionsResponse(items.Count, items));
    }

    private static async Task<IResult> CreateMeetingAsync(
        Guid organizationId,
        CreateLodgeMeetingRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();
        if (!LodgeManagementCodes.MeetingType.IsValid(request.MeetingType) ||
            !LodgeManagementCodes.Grade.IsValid(request.Grade))
        {
            return Results.BadRequest(new { message = "El tipo de tenida o grado indicado no es válido." });
        }

        var organizationExists = await institutionalDb.Organizations
            .AsNoTracking()
            .AnyAsync(x => x.Id == organizationId && x.Type == "workshop", cancellationToken);
        if (!organizationExists) return Results.NotFound(new { message = "El Taller indicado no existe." });

        var meeting = new LodgeMeeting
        {
            OrganizationId = organizationId,
            MeetingDate = request.MeetingDate,
            MeetingType = request.MeetingType,
            Grade = request.Grade,
            Title = NormalizeOptional(request.Title),
            Status = LodgeManagementCodes.MeetingStatus.Scheduled
        };

        db.LodgeMeetings.Add(meeting);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.meeting.created",
            nameof(LodgeMeeting),
            meeting.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { meeting.MeetingDate, meeting.MeetingType, meeting.Grade, meeting.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gestion-logial/tenidas/{meeting.Id}", ToMeetingDto(meeting));
    }

    private static async Task<IResult> GetMeetingsAsync(
        Guid organizationId,
        DateOnly? from,
        DateOnly? to,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();
        if (from is not null && to is not null && to < from)
            return Results.BadRequest(new { message = "La fecha final no puede ser anterior a la fecha inicial." });

        var query = db.LodgeMeetings.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (from is not null) query = query.Where(x => x.MeetingDate >= from.Value);
        if (to is not null) query = query.Where(x => x.MeetingDate <= to.Value);

        var meetings = await query
            .OrderByDescending(x => x.MeetingDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeMeetingsResponse(meetings.Count, meetings.Select(ToMeetingDto).ToList()));
    }

    private static async Task<IResult> CloseMeetingAsync(
        Guid meetingId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();
        if (meeting.Status == LodgeManagementCodes.MeetingStatus.Closed)
            return Results.Conflict(new { message = "La tenida ya se encuentra cerrada." });
        if (meeting.Status == LodgeManagementCodes.MeetingStatus.Cancelled)
            return Results.Conflict(new { message = "Una tenida cancelada no puede cerrarse." });

        meeting.Status = LodgeManagementCodes.MeetingStatus.Closed;
        meeting.ClosedAtUtc = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.meeting.closed",
            nameof(LodgeMeeting),
            meeting.Id.ToString(),
            meeting.OrganizationId,
            AuditResults.Success,
            new { meeting.MeetingDate, meeting.Status, meeting.ClosedAtUtc }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToMeetingDto(meeting));
    }

    private static async Task<IResult> RecordAttendanceAsync(
        Guid meetingId,
        LodgeAttendanceRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!LodgeManagementCodes.AttendanceStatus.IsValid(request.Status))
            return Results.BadRequest(new { message = "El estado de asistencia indicado no es válido." });

        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();
        if (meeting.Status != LodgeManagementCodes.MeetingStatus.Closed)
            return Results.Conflict(new { message = "La asistencia sólo puede registrarse después de cerrar la tenida realizada." });

        var memberBelongs = await institutionalDb.Memberships
            .AsNoTracking()
            .AnyAsync(x => x.MemberId == request.MemberId &&
                           x.OrganizationId == meeting.OrganizationId &&
                           x.EndDate == null &&
                           x.Status == MembershipCodes.MembershipStatus.Active,
                cancellationToken);
        if (!memberBelongs)
            return Results.BadRequest(new { message = "El hermano no registra una pertenencia activa al Taller de la tenida." });

        var record = new LodgeAttendanceRecord
        {
            MeetingId = meeting.Id,
            MemberId = request.MemberId,
            Status = request.Status,
            ExcuseReason = request.Status == LodgeManagementCodes.AttendanceStatus.Excused
                ? NormalizeOptional(request.ExcuseReason)
                : null
        };

        db.LodgeAttendanceRecords.Add(record);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.attendance.recorded",
            nameof(LodgeAttendanceRecord),
            record.Id.ToString(),
            meeting.OrganizationId,
            AuditResults.Success,
            new { meetingId = meeting.Id, record.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gestion-logial/tenidas/{meetingId}/asistencia/{record.Id}", new
        {
            record.Id,
            record.MeetingId,
            record.MemberId,
            record.Status,
            record.ExcuseReason,
            record.RecordedAtUtc
        });
    }

    private static async Task<IResult> GetCurrentAttendanceAsync(
        Guid meetingId,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();

        var rows = await db.LodgeAttendanceRecords
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var latest = rows.GroupBy(x => x.MemberId).Select(x => x.First()).ToList();
        var memberIds = latest.Select(x => x.MemberId).ToArray();
        var names = memberIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await institutionalDb.Members
                .AsNoTracking()
                .Where(x => memberIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    DisplayName = (x.Person.FirstNames + " " + x.Person.LastNames).Trim()
                })
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var items = latest
            .Select(x => new LodgeAttendanceCurrentDto(
                x.Id,
                x.MemberId,
                names.GetValueOrDefault(x.MemberId, "Hermano no disponible"),
                x.Status,
                x.ExcuseReason,
                x.RecordedAtUtc))
            .OrderBy(x => x.DisplayName)
            .ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeAttendanceResponse(items.Count, items));
    }

    private static async Task<IResult> RecordAnonymousBallotAsync(
        Guid meetingId,
        LodgeAnonymousBallotRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();
        if (meeting.Status != LodgeManagementCodes.MeetingStatus.Closed)
            return Results.Conflict(new { message = "El escrutinio sólo puede registrarse después de cerrar la Tenida realizada." });
        if (!LodgeManagementCodes.BallotType.IsValid(request.BallotType) || string.IsNullOrWhiteSpace(request.Subject))
            return Results.BadRequest(new { message = "La modalidad y el asunto de la votación son obligatorios." });
        if (request.EligibleCount < 0 || request.PositiveCount < 0 || request.NegativeCount < 0)
            return Results.BadRequest(new { message = "Las cantidades del escrutinio no pueden ser negativas." });

        var attendanceHistory = await db.LodgeAttendanceRecords.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        var attendeeCount = attendanceHistory.GroupBy(x => x.MemberId).Select(x => x.First())
            .Count(x => x.Status == LodgeManagementCodes.AttendanceStatus.Present);
        var counted = request.PositiveCount + request.NegativeCount;
        if (request.EligibleCount > attendeeCount)
            return Results.BadRequest(new { message = "Las personas habilitadas no pueden superar a las asistentes presentes." });
        var hasDifference = counted != request.EligibleCount;
        if (hasDifference && string.IsNullOrWhiteSpace(request.RecountObservation))
            return Results.BadRequest(new { message = "La diferencia entre habilitados y votos o balotas contabilizados debe explicarse en el acta." });

        var subject = request.Subject.Trim();
        var previous = await db.LodgeAnonymousBallots
            .Where(x => x.MeetingId == meetingId && x.Subject == subject && x.Status == LodgeManagementCodes.BallotStatus.Closed)
            .ToListAsync(cancellationToken);
        foreach (var item in previous) item.Status = LodgeManagementCodes.BallotStatus.Superseded;
        var version = previous.Count == 0 ? 1 : previous.Max(x => x.Version) + 1;
        var ballot = new LodgeAnonymousBallot
        {
            MeetingId = meetingId, Version = version, BallotType = request.BallotType, Subject = subject,
            AttendeeCount = attendeeCount, EligibleCount = request.EligibleCount,
            PositiveCount = request.PositiveCount, NegativeCount = request.NegativeCount,
            RecountObservation = NormalizeOptional(request.RecountObservation), Status = LodgeManagementCodes.BallotStatus.Closed,
            RecordedBySubject = GetSubject(httpContext.User)
        };
        db.LodgeAnonymousBallots.Add(ballot);
        db.AuditEvents.Add(AuditEventFactory.Create(httpContext, "lodge.anonymous_ballot.recorded", nameof(LodgeAnonymousBallot), ballot.Id.ToString(), meeting.OrganizationId, AuditResults.Success,
            new { ballot.MeetingId, ballot.Version, ballot.BallotType, ballot.Subject, ballot.AttendeeCount, ballot.EligibleCount, ballot.PositiveCount, ballot.NegativeCount, counted, hasDifference }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gestion-logial/tenidas/{meetingId}/votaciones/{ballot.Id}", ToBallotDto(ballot));
    }

    private static async Task<IResult> GetAnonymousBallotsAsync(
        Guid meetingId, HttpContext httpContext, LodgeManagementDbContext db,
        IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();
        var items = await db.LodgeAnonymousBallots.AsNoTracking().Where(x => x.MeetingId == meetingId)
            .OrderByDescending(x => x.RecordedAtUtc).Take(100).ToListAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeAnonymousBallotsResponse(items.Count, items.Select(ToBallotDto).ToList()));
    }

    private static async Task<IResult> GenerateMinuteExtractAsync(
        Guid meetingId, HttpContext httpContext, PmgmDbContext institutionalDb, LodgeManagementDbContext db,
        IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();

        var organization = await institutionalDb.Organizations.AsNoTracking()
            .Where(x => x.Id == meeting.OrganizationId).Select(x => new { x.Name, x.Number }).SingleAsync(cancellationToken);
        var attendanceHistory = await db.LodgeAttendanceRecords.AsNoTracking().Where(x => x.MeetingId == meetingId)
            .OrderByDescending(x => x.RecordedAtUtc).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
        var attendance = attendanceHistory.GroupBy(x => x.MemberId).Select(x => x.First()).ToList();
        var memberIds = attendance.Select(x => x.MemberId).ToArray();
        var names = await institutionalDb.Members.AsNoTracking().Where(x => memberIds.Contains(x.Id))
            .Select(x => new { x.Id, Name = (x.Person.FirstNames + " " + x.Person.LastNames).Trim() })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var ballots = await db.LodgeAnonymousBallots.AsNoTracking()
            .Where(x => x.MeetingId == meetingId && x.Status == LodgeManagementCodes.BallotStatus.Closed)
            .OrderBy(x => x.RecordedAtUtc).ToListAsync(cancellationToken);

        var present = attendance.Where(x => x.Status == LodgeManagementCodes.AttendanceStatus.Present).ToList();
        var absent = attendance.Where(x => x.Status == LodgeManagementCodes.AttendanceStatus.Absent).ToList();
        var excused = attendance.Where(x => x.Status == LodgeManagementCodes.AttendanceStatus.Excused).ToList();
        var ballotLines = ballots.Count == 0 ? "Sin balotajes o votaciones registrados."
            : string.Join(Environment.NewLine, ballots.Select((x, index) =>
                $"{index + 1}. {x.Subject}: {BallotPositiveLabel(x.BallotType)} {x.PositiveCount}; {BallotNegativeLabel(x.BallotType)} {x.NegativeCount}; habilitados {x.EligibleCount}; contabilizados {x.PositiveCount + x.NegativeCount}." +
                (string.IsNullOrWhiteSpace(x.RecountObservation) ? "" : $" Observación: {x.RecountObservation}")));
        var content = $"EXTRACTO DE ACTA{Environment.NewLine}" +
            $"Taller: {organization.Name}{(organization.Number is null ? "" : $" N.º {organization.Number}")}{Environment.NewLine}" +
            $"Fecha: {meeting.MeetingDate:dd-MM-yyyy} · Tipo: {meeting.MeetingType} · Grado: {meeting.Grade}{Environment.NewLine}{Environment.NewLine}" +
            $"ASISTENCIA{Environment.NewLine}Presentes: {present.Count} · Inasistentes: {absent.Count} · Excusados: {excused.Count} · Total registrado: {attendance.Count}{Environment.NewLine}" +
            $"Presentes: {JoinNames(present, names)}{Environment.NewLine}Excusas: {JoinNames(excused, names)}{Environment.NewLine}{Environment.NewLine}" +
            $"BALOTAJE Y VOTACIONES{Environment.NewLine}{ballotLines}{Environment.NewLine}{Environment.NewLine}" +
            "Apertura: __________ · Acta anterior: __________ · Correspondencia: __________ · Decretos: __________" + Environment.NewLine +
            "Trabajo presentado: __________ · Aportes: __________ · Bien general: __________" + Environment.NewLine +
            "Tronco de beneficencia: __________ · Clausura: __________ · Cierre de cadena: __________" + Environment.NewLine +
            "Firmas: Venerable Maestro/a · Secretario/a · Orador/a";

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { meetingId, attendeeCount = present.Count, absentCount = absent.Count, excusedCount = excused.Count, ballotCount = ballots.Count, content });
    }

    private static string JoinNames(IEnumerable<LodgeAttendanceRecord> rows, IReadOnlyDictionary<Guid, string> names)
    {
        var values = rows.Select(x => names.GetValueOrDefault(x.MemberId, "Hermano no disponible")).ToArray();
        return values.Length == 0 ? "Sin registros" : string.Join(", ", values);
    }

    private static string BallotPositiveLabel(string type) => type == LodgeManagementCodes.BallotType.WhiteBlack ? "blancas" : "positivos";
    private static string BallotNegativeLabel(string type) => type == LodgeManagementCodes.BallotType.WhiteBlack ? "negras" : "negativos";

    private static async Task<IResult> CreateMinuteVersionAsync(
        Guid meetingId,
        LodgeMinuteVersionRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.Content))
            return Results.BadRequest(new { message = "El contenido del acta es obligatorio." });

        var currentVersion = await db.LodgeMinutes
            .Where(x => x.MeetingId == meetingId)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken) ?? 0;

        var minute = new LodgeMinute
        {
            MeetingId = meeting.Id,
            Version = currentVersion + 1,
            Content = request.Content.Trim(),
            Status = LodgeManagementCodes.MinuteStatus.Draft,
            CreatedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeMinutes.Add(minute);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.minute.version_created",
            nameof(LodgeMinute),
            minute.Id.ToString(),
            meeting.OrganizationId,
            AuditResults.Success,
            new { meetingId = meeting.Id, minute.Version, minute.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gestion-logial/tenidas/{meetingId}/actas/{minute.Id}", ToMinuteDto(minute));
    }

    private static async Task<IResult> GetMinutesAsync(
        Guid meetingId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();

        var minutes = await db.LodgeMinutes
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderByDescending(x => x.Version)
            .Take(100)
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeMinutesResponse(minutes.Count, minutes.Select(ToMinuteDto).ToList()));
    }

    private static async Task<IResult> ApproveMinuteAsync(
        Guid meetingId,
        Guid minuteId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();

        var minute = await db.LodgeMinutes.SingleOrDefaultAsync(x => x.Id == minuteId && x.MeetingId == meetingId, cancellationToken);
        if (minute is null) return Results.NotFound(new { message = "La versión de acta indicada no existe." });
        if (minute.Status == LodgeManagementCodes.MinuteStatus.Approved)
            return Results.Conflict(new { message = "Esta versión del acta ya está aprobada." });
        if (minute.Status == LodgeManagementCodes.MinuteStatus.Superseded)
            return Results.Conflict(new { message = "Una versión reemplazada no puede aprobarse nuevamente." });

        var previousApproved = await db.LodgeMinutes
            .Where(x => x.MeetingId == meetingId && x.Status == LodgeManagementCodes.MinuteStatus.Approved)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousApproved)
        {
            previous.Status = LodgeManagementCodes.MinuteStatus.Superseded;
        }

        minute.Status = LodgeManagementCodes.MinuteStatus.Approved;
        minute.ApprovedAtUtc = DateTimeOffset.UtcNow;
        minute.ApprovedBySubject = GetSubject(httpContext.User);

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.minute.approved",
            nameof(LodgeMinute),
            minute.Id.ToString(),
            meeting.OrganizationId,
            AuditResults.Success,
            new { meetingId = meeting.Id, minute.Version, minute.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToMinuteDto(minute));
    }

    private static LodgeMeetingDto ToMeetingDto(LodgeMeeting meeting)
        => new(meeting.Id, meeting.OrganizationId, meeting.MeetingDate, meeting.MeetingType, meeting.Grade, meeting.Title, meeting.Status, meeting.CreatedAtUtc, meeting.ClosedAtUtc);

    private static LodgeMinuteDto ToMinuteDto(LodgeMinute minute)
        => new(minute.Id, minute.MeetingId, minute.Version, minute.Content, minute.Status, minute.CreatedAtUtc, minute.ApprovedAtUtc);

    private static LodgeAnonymousBallotDto ToBallotDto(LodgeAnonymousBallot ballot)
        => new(ballot.Id, ballot.MeetingId, ballot.Version, ballot.BallotType, ballot.Subject, ballot.AttendeeCount,
            ballot.EligibleCount, ballot.PositiveCount, ballot.NegativeCount, ballot.RecountObservation, ballot.Status, ballot.RecordedAtUtc);

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "institutional-user";

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CreateLodgeMeetingRequest(
    DateOnly MeetingDate,
    string MeetingType,
    string Grade,
    string? Title);

public sealed record LodgeAttendanceRequest(
    Guid MemberId,
    string Status,
    string? ExcuseReason);

public sealed record LodgeMinuteVersionRequest(string Content);
public sealed record LodgeAnonymousBallotRequest(string BallotType, string Subject, int EligibleCount, int PositiveCount, int NegativeCount, string? RecountObservation);

public sealed record LodgeMemberOptionDto(Guid Id, string DisplayName);
public sealed record LodgeMemberOptionsResponse(int Total, IReadOnlyList<LodgeMemberOptionDto> Items);

public sealed record LodgeMeetingDto(
    Guid Id,
    Guid OrganizationId,
    DateOnly MeetingDate,
    string MeetingType,
    string Grade,
    string? Title,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ClosedAtUtc);
public sealed record LodgeMeetingsResponse(int Total, IReadOnlyList<LodgeMeetingDto> Items);

public sealed record LodgeAttendanceCurrentDto(
    Guid RecordId,
    Guid MemberId,
    string DisplayName,
    string Status,
    string? ExcuseReason,
    DateTimeOffset RecordedAtUtc);
public sealed record LodgeAttendanceResponse(int Total, IReadOnlyList<LodgeAttendanceCurrentDto> Items);

public sealed record LodgeMinuteDto(
    Guid Id,
    Guid MeetingId,
    int Version,
    string Content,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc);
public sealed record LodgeMinutesResponse(int Total, IReadOnlyList<LodgeMinuteDto> Items);
public sealed record LodgeAnonymousBallotDto(Guid Id, Guid MeetingId, int Version, string BallotType, string Subject,
    int AttendeeCount, int EligibleCount, int PositiveCount, int NegativeCount, string? RecountObservation, string Status, DateTimeOffset RecordedAtUtc);
public sealed record LodgeAnonymousBallotsResponse(int Total, IReadOnlyList<LodgeAnonymousBallotDto> Items);
