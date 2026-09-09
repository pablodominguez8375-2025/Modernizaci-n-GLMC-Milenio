using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.LodgeManagement;

public static class LodgeInstructionEndpoints
{
    public static IEndpointRouteBuilder MapLodgeInstructionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial")
            .WithTags("Gestión Logial — Instrucción")
            .RequireAuthorization();

        group.MapPost("/talleres/{organizationId:guid}/instrucciones", CreateInstructionAsync);
        group.MapGet("/talleres/{organizationId:guid}/instrucciones", GetInstructionsAsync);
        group.MapPost("/instrucciones/{instructionId:guid}/asistencia", RecordInstructionAttendanceAsync);
        group.MapGet("/miembros/{memberId:guid}/instrucciones", GetMemberInstructionHistoryAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateInstructionAsync(
        Guid organizationId,
        CreateLodgeInstructionRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();

        var grade = request.Grade?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!LodgeManagementCodes.Grade.IsValid(grade) || grade == LodgeManagementCodes.Grade.All)
            return Results.BadRequest(new { message = "La instrucción debe corresponder a un grado específico." });

        var responsibleOffice = LodgeManagementCodes.InstructionOffice.ForGrade(grade);
        if (responsibleOffice is null)
            return Results.BadRequest(new { message = "No existe un cargo responsable configurado para el grado indicado." });

        var topic = NormalizeRequired(request.Topic, 500);
        if (topic is null)
            return Results.BadRequest(new { message = "El tema de la instrucción es obligatorio y no puede exceder 500 caracteres." });

        var organizationExists = await institutionalDb.Organizations
            .AsNoTracking()
            .AnyAsync(x => x.Id == organizationId && x.Type == "workshop", cancellationToken);
        if (!organizationExists) return Results.NotFound(new { message = "El Taller indicado no existe." });

        if (request.InstructorMemberId is not null)
        {
            var instructorBelongs = await institutionalDb.Memberships
                .AsNoTracking()
                .AnyAsync(x => x.MemberId == request.InstructorMemberId.Value &&
                               x.OrganizationId == organizationId &&
                               x.EndDate == null &&
                               x.Status == MembershipCodes.MembershipStatus.Active,
                    cancellationToken);
            if (!instructorBelongs)
                return Results.BadRequest(new { message = "El instructor indicado no registra pertenencia activa al Taller." });
        }

        var instruction = new LodgeInstructionSession
        {
            OrganizationId = organizationId,
            InstructionDate = request.InstructionDate,
            Grade = grade,
            Topic = topic,
            ResponsibleOffice = responsibleOffice,
            InstructorMemberId = request.InstructorMemberId,
            Status = LodgeManagementCodes.InstructionStatus.Held,
            CreatedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeInstructionSessions.Add(instruction);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.instruction.created",
            nameof(LodgeInstructionSession),
            instruction.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new
            {
                instruction.InstructionDate,
                instruction.Grade,
                instruction.ResponsibleOffice,
                instruction.Status,
                HasInstructor = instruction.InstructorMemberId is not null
            }));
        await db.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Created(
            $"/api/gestion-logial/instrucciones/{instruction.Id}",
            ToInstructionDto(instruction));
    }

    private static async Task<IResult> GetInstructionsAsync(
        Guid organizationId,
        DateOnly? from,
        DateOnly? to,
        string? grade,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();
        if (from is not null && to is not null && to < from)
            return Results.BadRequest(new { message = "La fecha final no puede ser anterior a la fecha inicial." });

        var normalizedGrade = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim().ToLowerInvariant();
        if (normalizedGrade is not null &&
            (!LodgeManagementCodes.Grade.IsValid(normalizedGrade) || normalizedGrade == LodgeManagementCodes.Grade.All))
        {
            return Results.BadRequest(new { message = "El grado de filtro no es válido." });
        }

        var query = db.LodgeInstructionSessions
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId);

        if (from is not null) query = query.Where(x => x.InstructionDate >= from.Value);
        if (to is not null) query = query.Where(x => x.InstructionDate <= to.Value);
        if (normalizedGrade is not null) query = query.Where(x => x.Grade == normalizedGrade);

        var items = await query
            .OrderByDescending(x => x.InstructionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(250)
            .Select(x => new LodgeInstructionDto(
                x.Id,
                x.OrganizationId,
                x.InstructionDate,
                x.Grade,
                x.Topic,
                x.ResponsibleOffice,
                x.InstructorMemberId,
                x.Status))
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new LodgeInstructionsResponse(items.Count, items));
    }

    private static async Task<IResult> RecordInstructionAttendanceAsync(
        Guid instructionId,
        LodgeInstructionAttendanceBatchRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest(new { message = "Debe informar al menos un hermano para registrar asistencia." });
        if (request.Items.Count > 500)
            return Results.BadRequest(new { message = "No se pueden registrar más de 500 asistencias en una sola operación." });

        var instruction = await db.LodgeInstructionSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == instructionId, cancellationToken);
        if (instruction is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, instruction.OrganizationId)) return Results.Forbid();
        if (instruction.Status == LodgeManagementCodes.InstructionStatus.Cancelled)
            return Results.Conflict(new { message = "No se puede registrar asistencia en una instrucción cancelada." });

        if (request.Items.Any(x => !LodgeManagementCodes.InstructionAttendanceStatus.IsValid(x.Status)))
            return Results.BadRequest(new { message = "Existe un estado de asistencia no válido." });

        var duplicateMember = request.Items
            .GroupBy(x => x.MemberId)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateMember is not null)
            return Results.BadRequest(new { message = "Un hermano no puede repetirse dentro del mismo registro de asistencia." });

        var memberIds = request.Items.Select(x => x.MemberId).ToArray();
        var activeMemberIds = await institutionalDb.Memberships
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) &&
                        x.OrganizationId == instruction.OrganizationId &&
                        x.EndDate == null &&
                        x.Status == MembershipCodes.MembershipStatus.Active)
            .Select(x => x.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (activeMemberIds.Count != memberIds.Length)
            return Results.BadRequest(new { message = "Todos los hermanos informados deben tener pertenencia activa al Taller." });

        var degreeEvents = await institutionalDb.DegreeEvents
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.EffectiveDate <= instruction.InstructionDate)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.MemberId, x.Degree, x.EffectiveDate, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);

        var effectiveDegrees = degreeEvents
            .GroupBy(x => x.MemberId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => InstitutionalDegree.TryParse(x.Degree, out var parsed) ? parsed : (int?)null)
                    .FirstOrDefault(x => x is not null));

        var expectedDegree = LodgeManagementCodes.Grade.ToNumeric(instruction.Grade);
        if (expectedDegree is null)
            return Results.Conflict(new { message = "La instrucción no tiene un grado institucional válido." });

        foreach (var memberId in memberIds)
        {
            if (!effectiveDegrees.TryGetValue(memberId, out var memberDegree) || memberDegree is null)
                return Results.BadRequest(new { message = "No fue posible determinar el grado institucional vigente de uno de los hermanos." });

            var matches = expectedDegree.Value == 3
                ? memberDegree.Value >= 3
                : memberDegree.Value == expectedDegree.Value;
            if (!matches)
                return Results.BadRequest(new { message = "Uno de los hermanos no corresponde al grado de la instrucción." });
        }

        var recordedBy = GetSubject(httpContext.User);
        foreach (var item in request.Items)
        {
            db.LodgeInstructionAttendanceRecords.Add(new LodgeInstructionAttendanceRecord
            {
                InstructionSessionId = instruction.Id,
                MemberId = item.MemberId,
                Status = item.Status,
                RecordedBySubject = recordedBy
            });
        }

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.instruction.attendance_recorded",
            nameof(LodgeInstructionAttendanceRecord),
            instruction.Id.ToString(),
            instruction.OrganizationId,
            AuditResults.Success,
            new
            {
                InstructionSessionId = instruction.Id,
                Count = request.Items.Count,
                Present = request.Items.Count(x => x.Status == LodgeManagementCodes.InstructionAttendanceStatus.Present),
                Absent = request.Items.Count(x => x.Status == LodgeManagementCodes.InstructionAttendanceStatus.Absent)
            }));
        await db.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            instructionId = instruction.Id,
            recorded = request.Items.Count
        });
    }

    private static async Task<IResult> GetMemberInstructionHistoryAsync(
        Guid memberId,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        IInstitutionalMemberContextResolver memberContextResolver,
        CancellationToken cancellationToken)
    {
        var actor = await memberContextResolver.ResolveAsync(httpContext.User, cancellationToken);
        var self = actor?.MemberId == memberId;

        if (!self)
        {
            var organizationIds = await institutionalDb.Memberships
                .AsNoTracking()
                .Where(x => x.MemberId == memberId)
                .Select(x => x.OrganizationId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!organizationIds.Any(x => access.CanManageOrganization(httpContext.User, x)))
                return Results.Forbid();
        }

        var memberExists = await institutionalDb.Members
            .AsNoTracking()
            .AnyAsync(x => x.Id == memberId, cancellationToken);
        if (!memberExists) return Results.NotFound();

        var attendanceEvents = await db.LodgeInstructionAttendanceRecords
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Take(2000)
            .Select(x => new
            {
                x.InstructionSessionId,
                x.Status,
                x.RecordedAtUtc
            })
            .ToListAsync(cancellationToken);

        var currentAttendance = attendanceEvents
            .GroupBy(x => x.InstructionSessionId)
            .Select(group => group.OrderByDescending(x => x.RecordedAtUtc).First())
            .ToDictionary(x => x.InstructionSessionId);

        if (currentAttendance.Count == 0)
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new MemberInstructionHistoryResponse(memberId, 0, Array.Empty<MemberInstructionHistoryItemDto>()));
        }

        var instructionIds = currentAttendance.Keys.ToArray();
        var sessions = await db.LodgeInstructionSessions
            .AsNoTracking()
            .Where(x => instructionIds.Contains(x.Id))
            .OrderByDescending(x => x.InstructionDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var items = sessions
            .Select(session =>
            {
                var attendance = currentAttendance[session.Id];
                return new MemberInstructionHistoryItemDto(
                    session.Id,
                    session.InstructionDate,
                    session.Grade,
                    session.Topic,
                    attendance.Status == LodgeManagementCodes.InstructionAttendanceStatus.Present,
                    attendance.Status);
            })
            .ToArray();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new MemberInstructionHistoryResponse(memberId, items.Length, items));
    }

    private static LodgeInstructionDto ToInstructionDto(LodgeInstructionSession value)
        => new(
            value.Id,
            value.OrganizationId,
            value.InstructionDate,
            value.Grade,
            value.Topic,
            value.ResponsibleOffice,
            value.InstructorMemberId,
            value.Status);

    private static string? NormalizeRequired(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength ? null : normalized;
    }

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub")
           ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? user.Identity?.Name
           ?? "authenticated-user";
}

public sealed record CreateLodgeInstructionRequest(
    DateOnly InstructionDate,
    string? Grade,
    string? Topic,
    Guid? InstructorMemberId);

public sealed record LodgeInstructionAttendanceItemRequest(Guid MemberId, string Status);

public sealed record LodgeInstructionAttendanceBatchRequest(
    IReadOnlyCollection<LodgeInstructionAttendanceItemRequest> Items);

public sealed record LodgeInstructionDto(
    Guid Id,
    Guid OrganizationId,
    DateOnly InstructionDate,
    string Grade,
    string Topic,
    string ResponsibleOffice,
    Guid? InstructorMemberId,
    string Status);

public sealed record LodgeInstructionsResponse(int Total, IReadOnlyCollection<LodgeInstructionDto> Items);

public sealed record MemberInstructionHistoryItemDto(
    Guid InstructionId,
    DateOnly InstructionDate,
    string Grade,
    string Topic,
    bool Attended,
    string Status);

public sealed record MemberInstructionHistoryResponse(
    Guid MemberId,
    int Total,
    IReadOnlyCollection<MemberInstructionHistoryItemDto> Items);
