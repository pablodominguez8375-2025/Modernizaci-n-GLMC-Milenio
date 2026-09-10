using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.LodgeManagement.Entities;

namespace PMGM.Api.Modules.LodgeManagement;

public static class LodgeSecretariatEndpoints
{
    public static IEndpointRouteBuilder MapLodgeSecretariatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial")
            .WithTags("Secretaría Logial")
            .RequireAuthorization();

        group.MapGet("/talleres/{organizationId:guid}/secretaria/correspondencia", GetCorrespondenceAsync);
        group.MapPost("/talleres/{organizationId:guid}/secretaria/correspondencia", CreateCorrespondenceAsync);
        group.MapPost("/talleres/{organizationId:guid}/secretaria/correspondencia/{recordId:guid}/estado", UpdateCorrespondenceStatusAsync);

        group.MapGet("/talleres/{organizationId:guid}/secretaria/pendientes", GetTasksAsync);
        group.MapPost("/talleres/{organizationId:guid}/secretaria/pendientes", CreateTaskAsync);
        group.MapPost("/talleres/{organizationId:guid}/secretaria/pendientes/{taskId:guid}/estado", UpdateTaskStatusAsync);

        group.MapGet("/tenidas/{meetingId:guid}/tabla", GetAgendaAsync);
        group.MapPost("/tenidas/{meetingId:guid}/tabla", CreateAgendaItemAsync);
        group.MapPost("/tenidas/{meetingId:guid}/tabla/{itemId:guid}/estado", UpdateAgendaStatusAsync);

        return endpoints;
    }

    private static async Task<IResult> GetCorrespondenceAsync(
        Guid organizationId,
        string? direction,
        string? status,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();

        var normalizedDirection = NormalizeOptional(direction)?.ToLowerInvariant();
        var normalizedStatus = NormalizeOptional(status)?.ToLowerInvariant();
        if (normalizedDirection is not null && !LodgeSecretariatCodes.CorrespondenceDirection.IsValid(normalizedDirection))
            return Results.BadRequest(new { message = "La dirección de correspondencia indicada no es válida." });
        if (normalizedStatus is not null && !LodgeSecretariatCodes.CorrespondenceStatus.IsValid(normalizedStatus))
            return Results.BadRequest(new { message = "El estado de correspondencia indicado no es válido." });

        var query = db.LodgeCorrespondenceRecords.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (normalizedDirection is not null) query = query.Where(x => x.Direction == normalizedDirection);
        if (normalizedStatus is not null) query = query.Where(x => x.Status == normalizedStatus);

        var items = await query
            .OrderByDescending(x => x.CorrespondenceDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .Select(x => new LodgeCorrespondenceDto(
                x.Id, x.OrganizationId, x.Folio, x.Direction, x.CorrespondenceDate, x.Subject,
                x.Counterparty, x.Channel, x.ExternalReference, x.Status, x.Notes, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        SetPrivateNoStore(httpContext);
        return Results.Ok(new LodgeCorrespondenceResponse(items.Count, items));
    }

    private static async Task<IResult> CreateCorrespondenceAsync(
        Guid organizationId,
        CreateLodgeCorrespondenceRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();
        var validation = ValidateCorrespondence(request);
        if (validation is not null) return Results.BadRequest(new { message = validation });

        var organizationExists = await institutionalDb.Organizations.AsNoTracking()
            .AnyAsync(x => x.Id == organizationId && x.Type == "workshop", cancellationToken);
        if (!organizationExists) return Results.NotFound(new { message = "El Taller indicado no existe." });

        var folio = request.Folio.Trim();
        var folioExists = await db.LodgeCorrespondenceRecords.AsNoTracking()
            .AnyAsync(x => x.OrganizationId == organizationId && x.Folio == folio, cancellationToken);
        if (folioExists) return Results.Conflict(new { message = "El folio ya existe en este Taller." });

        var record = new LodgeCorrespondenceRecord
        {
            OrganizationId = organizationId,
            Folio = folio,
            Direction = request.Direction.Trim().ToLowerInvariant(),
            CorrespondenceDate = request.CorrespondenceDate,
            Subject = request.Subject.Trim(),
            Counterparty = request.Counterparty.Trim(),
            Channel = request.Channel.Trim().ToLowerInvariant(),
            ExternalReference = NormalizeOptional(request.ExternalReference),
            Status = LodgeSecretariatCodes.CorrespondenceStatus.Registered,
            Notes = NormalizeOptional(request.Notes),
            CreatedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeCorrespondenceRecords.Add(record);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.secretariat.correspondence.created",
            nameof(LodgeCorrespondenceRecord),
            record.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { record.Folio, record.Direction, record.CorrespondenceDate, record.Channel, record.Status }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/gestion-logial/talleres/{organizationId}/secretaria/correspondencia/{record.Id}", ToCorrespondenceDto(record));
    }

    private static async Task<IResult> UpdateCorrespondenceStatusAsync(
        Guid organizationId,
        Guid recordId,
        UpdateLodgeSecretariatStatusRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();
        var next = request.Status.Trim().ToLowerInvariant();
        if (!LodgeSecretariatCodes.CorrespondenceStatus.IsValid(next))
            return Results.BadRequest(new { message = "El estado de correspondencia indicado no es válido." });

        var record = await db.LodgeCorrespondenceRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.OrganizationId == organizationId, cancellationToken);
        if (record is null) return Results.NotFound();
        if (!LodgeSecretariatCodes.CorrespondenceStatus.CanTransition(record.Status, next))
            return Results.Conflict(new { message = "La transición de estado indicada no está permitida." });

        record.Status = next;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;
        record.UpdatedBySubject = GetSubject(httpContext.User);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.secretariat.correspondence.status_changed",
            nameof(LodgeCorrespondenceRecord),
            record.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { record.Folio, record.Status }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToCorrespondenceDto(record));
    }

    private static async Task<IResult> GetTasksAsync(
        Guid organizationId,
        string? status,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();
        var normalizedStatus = NormalizeOptional(status)?.ToLowerInvariant();
        if (normalizedStatus is not null && !LodgeSecretariatCodes.TaskStatus.IsValid(normalizedStatus))
            return Results.BadRequest(new { message = "El estado de pendiente indicado no es válido." });

        var query = db.LodgeSecretariatTasks.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (normalizedStatus is not null) query = query.Where(x => x.Status == normalizedStatus);

        var items = await query
            .OrderBy(x => x.Status == LodgeSecretariatCodes.TaskStatus.Open ? 0 : 1)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .Select(x => new LodgeSecretariatTaskDto(
                x.Id, x.OrganizationId, x.Title, x.Detail, x.DueDate, x.Priority, x.ResponsibleLabel,
                x.Status, x.CreatedAtUtc, x.CompletedAtUtc))
            .ToListAsync(cancellationToken);

        SetPrivateNoStore(httpContext);
        return Results.Ok(new LodgeSecretariatTasksResponse(items.Count, items));
    }

    private static async Task<IResult> CreateTaskAsync(
        Guid organizationId,
        CreateLodgeSecretariatTaskRequest request,
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();
        var validation = ValidateTask(request);
        if (validation is not null) return Results.BadRequest(new { message = validation });

        var organizationExists = await institutionalDb.Organizations.AsNoTracking()
            .AnyAsync(x => x.Id == organizationId && x.Type == "workshop", cancellationToken);
        if (!organizationExists) return Results.NotFound(new { message = "El Taller indicado no existe." });

        var task = new LodgeSecretariatTask
        {
            OrganizationId = organizationId,
            Title = request.Title.Trim(),
            Detail = NormalizeOptional(request.Detail),
            DueDate = request.DueDate,
            Priority = request.Priority.Trim().ToLowerInvariant(),
            ResponsibleLabel = NormalizeOptional(request.ResponsibleLabel),
            Status = LodgeSecretariatCodes.TaskStatus.Open,
            CreatedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeSecretariatTasks.Add(task);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.secretariat.task.created",
            nameof(LodgeSecretariatTask),
            task.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { task.DueDate, task.Priority, task.Status }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gestion-logial/talleres/{organizationId}/secretaria/pendientes/{task.Id}", ToTaskDto(task));
    }

    private static async Task<IResult> UpdateTaskStatusAsync(
        Guid organizationId,
        Guid taskId,
        UpdateLodgeSecretariatStatusRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, organizationId)) return Results.Forbid();
        var next = request.Status.Trim().ToLowerInvariant();
        if (!LodgeSecretariatCodes.TaskStatus.IsValid(next))
            return Results.BadRequest(new { message = "El estado de pendiente indicado no es válido." });

        var task = await db.LodgeSecretariatTasks.SingleOrDefaultAsync(x => x.Id == taskId && x.OrganizationId == organizationId, cancellationToken);
        if (task is null) return Results.NotFound();
        if (!LodgeSecretariatCodes.TaskStatus.CanTransition(task.Status, next))
            return Results.Conflict(new { message = "El pendiente ya se encuentra en un estado final." });

        task.Status = next;
        if (next == LodgeSecretariatCodes.TaskStatus.Done || next == LodgeSecretariatCodes.TaskStatus.Cancelled)
        {
            task.CompletedAtUtc = DateTimeOffset.UtcNow;
            task.CompletedBySubject = GetSubject(httpContext.User);
        }

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.secretariat.task.status_changed",
            nameof(LodgeSecretariatTask),
            task.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { task.DueDate, task.Priority, task.Status }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToTaskDto(task));
    }

    private static async Task<IResult> GetAgendaAsync(
        Guid meetingId,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();

        var items = await db.LodgeMeetingAgendaItems.AsNoTracking()
            .Where(x => x.MeetingId == meetingId && x.OrganizationId == meeting.OrganizationId)
            .OrderBy(x => x.Position)
            .Select(x => new LodgeMeetingAgendaItemDto(x.Id, x.MeetingId, x.OrganizationId, x.Position, x.Title, x.Detail, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        SetPrivateNoStore(httpContext);
        return Results.Ok(new LodgeMeetingAgendaResponse(items.Count, items));
    }

    private static async Task<IResult> CreateAgendaItemAsync(
        Guid meetingId,
        CreateLodgeMeetingAgendaItemRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();
        if (meeting.Status == LodgeManagementCodes.MeetingStatus.Closed || meeting.Status == LodgeManagementCodes.MeetingStatus.Cancelled)
            return Results.Conflict(new { message = "No se puede modificar la tabla de una Tenida cerrada o cancelada." });
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 500)
            return Results.BadRequest(new { message = "El título del punto es obligatorio y admite hasta 500 caracteres." });
        if (NormalizeOptional(request.Detail)?.Length > 3000)
            return Results.BadRequest(new { message = "El detalle del punto admite hasta 3000 caracteres." });

        var nextPosition = await db.LodgeMeetingAgendaItems
            .Where(x => x.MeetingId == meetingId)
            .Select(x => (int?)x.Position)
            .MaxAsync(cancellationToken) ?? 0;

        var item = new LodgeMeetingAgendaItem
        {
            MeetingId = meeting.Id,
            OrganizationId = meeting.OrganizationId,
            Position = nextPosition + 1,
            Title = request.Title.Trim(),
            Detail = NormalizeOptional(request.Detail),
            Status = LodgeSecretariatCodes.AgendaStatus.Pending,
            CreatedBySubject = GetSubject(httpContext.User)
        };

        db.LodgeMeetingAgendaItems.Add(item);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.secretariat.agenda.created",
            nameof(LodgeMeetingAgendaItem),
            item.Id.ToString(),
            meeting.OrganizationId,
            AuditResults.Success,
            new { meetingId = meeting.Id, item.Position, item.Status }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gestion-logial/tenidas/{meetingId}/tabla/{item.Id}", ToAgendaDto(item));
    }

    private static async Task<IResult> UpdateAgendaStatusAsync(
        Guid meetingId,
        Guid itemId,
        UpdateLodgeSecretariatStatusRequest request,
        HttpContext httpContext,
        LodgeManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var meeting = await db.LodgeMeetings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == meetingId, cancellationToken);
        if (meeting is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, meeting.OrganizationId)) return Results.Forbid();
        var next = request.Status.Trim().ToLowerInvariant();
        if (!LodgeSecretariatCodes.AgendaStatus.IsValid(next))
            return Results.BadRequest(new { message = "El estado del punto de tabla no es válido." });

        var item = await db.LodgeMeetingAgendaItems.SingleOrDefaultAsync(
            x => x.Id == itemId && x.MeetingId == meetingId && x.OrganizationId == meeting.OrganizationId,
            cancellationToken);
        if (item is null) return Results.NotFound();
        if (!LodgeSecretariatCodes.AgendaStatus.CanTransition(item.Status, next))
            return Results.Conflict(new { message = "El punto de tabla ya se encuentra en un estado final." });

        item.Status = next;
        item.UpdatedAtUtc = DateTimeOffset.UtcNow;
        item.UpdatedBySubject = GetSubject(httpContext.User);
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "lodge.secretariat.agenda.status_changed",
            nameof(LodgeMeetingAgendaItem),
            item.Id.ToString(),
            meeting.OrganizationId,
            AuditResults.Success,
            new { meetingId = meeting.Id, item.Position, item.Status }));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToAgendaDto(item));
    }

    private static string? ValidateCorrespondence(CreateLodgeCorrespondenceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Folio) || request.Folio.Trim().Length > 80) return "El folio es obligatorio y admite hasta 80 caracteres.";
        var direction = request.Direction.Trim().ToLowerInvariant();
        if (!LodgeSecretariatCodes.CorrespondenceDirection.IsValid(direction)) return "La dirección de correspondencia no es válida.";
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length > 500) return "El asunto es obligatorio y admite hasta 500 caracteres.";
        if (string.IsNullOrWhiteSpace(request.Counterparty) || request.Counterparty.Trim().Length > 320) return "La contraparte es obligatoria y admite hasta 320 caracteres.";
        var channel = request.Channel.Trim().ToLowerInvariant();
        if (!LodgeSecretariatCodes.Channel.IsValid(channel)) return "El canal de correspondencia no es válido.";
        if (NormalizeOptional(request.ExternalReference)?.Length > 240) return "La referencia externa admite hasta 240 caracteres.";
        if (NormalizeOptional(request.Notes)?.Length > 2000) return "Las observaciones admiten hasta 2000 caracteres.";
        return null;
    }

    private static string? ValidateTask(CreateLodgeSecretariatTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 500) return "El título es obligatorio y admite hasta 500 caracteres.";
        if (NormalizeOptional(request.Detail)?.Length > 2000) return "El detalle admite hasta 2000 caracteres.";
        var priority = request.Priority.Trim().ToLowerInvariant();
        if (!LodgeSecretariatCodes.Priority.IsValid(priority)) return "La prioridad indicada no es válida.";
        if (NormalizeOptional(request.ResponsibleLabel)?.Length > 160) return "El responsable admite hasta 160 caracteres.";
        return null;
    }

    private static LodgeCorrespondenceDto ToCorrespondenceDto(LodgeCorrespondenceRecord x)
        => new(x.Id, x.OrganizationId, x.Folio, x.Direction, x.CorrespondenceDate, x.Subject, x.Counterparty, x.Channel, x.ExternalReference, x.Status, x.Notes, x.CreatedAtUtc, x.UpdatedAtUtc);

    private static LodgeSecretariatTaskDto ToTaskDto(LodgeSecretariatTask x)
        => new(x.Id, x.OrganizationId, x.Title, x.Detail, x.DueDate, x.Priority, x.ResponsibleLabel, x.Status, x.CreatedAtUtc, x.CompletedAtUtc);

    private static LodgeMeetingAgendaItemDto ToAgendaDto(LodgeMeetingAgendaItem x)
        => new(x.Id, x.MeetingId, x.OrganizationId, x.Position, x.Title, x.Detail, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc);

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "institutional-user";

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void SetPrivateNoStore(HttpContext httpContext)
        => httpContext.Response.Headers.CacheControl = "private, no-store";
}

public static class LodgeSecretariatCodes
{
    public static class CorrespondenceDirection
    {
        public const string Incoming = "incoming";
        public const string Outgoing = "outgoing";
        public static bool IsValid(string value) => value == Incoming || value == Outgoing;
    }

    public static class Channel
    {
        public const string Email = "email";
        public const string Letter = "letter";
        public const string Platform = "platform";
        public const string Other = "other";
        public static bool IsValid(string value) => value == Email || value == Letter || value == Platform || value == Other;
    }

    public static class CorrespondenceStatus
    {
        public const string Registered = "registered";
        public const string Processed = "processed";
        public const string Archived = "archived";
        public static bool IsValid(string value) => value == Registered || value == Processed || value == Archived;
        public static bool CanTransition(string current, string next)
            => current == next
               || (current == Registered && (next == Processed || next == Archived))
               || (current == Processed && next == Archived);
    }

    public static class Priority
    {
        public const string Low = "low";
        public const string Normal = "normal";
        public const string High = "high";
        public static bool IsValid(string value) => value == Low || value == Normal || value == High;
    }

    public static class TaskStatus
    {
        public const string Open = "open";
        public const string Done = "done";
        public const string Cancelled = "cancelled";
        public static bool IsValid(string value) => value == Open || value == Done || value == Cancelled;
        public static bool CanTransition(string current, string next)
            => current == next || (current == Open && (next == Done || next == Cancelled));
    }

    public static class AgendaStatus
    {
        public const string Pending = "pending";
        public const string Addressed = "addressed";
        public const string Deferred = "deferred";
        public static bool IsValid(string value) => value == Pending || value == Addressed || value == Deferred;
        public static bool CanTransition(string current, string next)
            => current == next || (current == Pending && (next == Addressed || next == Deferred));
    }
}

public sealed record CreateLodgeCorrespondenceRequest(
    string Folio,
    string Direction,
    DateOnly CorrespondenceDate,
    string Subject,
    string Counterparty,
    string Channel,
    string? ExternalReference,
    string? Notes);

public sealed record UpdateLodgeSecretariatStatusRequest(string Status);

public sealed record LodgeCorrespondenceDto(
    Guid Id,
    Guid OrganizationId,
    string Folio,
    string Direction,
    DateOnly CorrespondenceDate,
    string Subject,
    string Counterparty,
    string Channel,
    string? ExternalReference,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record LodgeCorrespondenceResponse(int Total, IReadOnlyList<LodgeCorrespondenceDto> Items);

public sealed record CreateLodgeSecretariatTaskRequest(
    string Title,
    string? Detail,
    DateOnly? DueDate,
    string Priority,
    string? ResponsibleLabel);

public sealed record LodgeSecretariatTaskDto(
    Guid Id,
    Guid OrganizationId,
    string Title,
    string? Detail,
    DateOnly? DueDate,
    string Priority,
    string? ResponsibleLabel,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record LodgeSecretariatTasksResponse(int Total, IReadOnlyList<LodgeSecretariatTaskDto> Items);

public sealed record CreateLodgeMeetingAgendaItemRequest(string Title, string? Detail);

public sealed record LodgeMeetingAgendaItemDto(
    Guid Id,
    Guid MeetingId,
    Guid OrganizationId,
    int Position,
    string Title,
    string? Detail,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record LodgeMeetingAgendaResponse(int Total, IReadOnlyList<LodgeMeetingAgendaItemDto> Items);
