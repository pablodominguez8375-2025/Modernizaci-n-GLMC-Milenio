using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;

namespace PMGM.Api.Modules.InstitutionalCalendar;

public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapInstitutionalCalendarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/calendar")
            .WithTags("Calendario institucional")
            .RequireAuthorization();

        group.MapGet("", GetCalendarAsync);
        group.MapGet("/ics", ExportIcsAsync);
        group.MapPost("/manual", CreateManualAsync);
        group.MapPut("/source", UpsertSourceAsync);
        group.MapPost("/{eventId:guid}/cancel", CancelManualAsync);

        return endpoints;
    }

    private static async Task<IResult> GetCalendarAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? organizationId,
        HttpContext httpContext,
        CalendarDbContext db,
        IInstitutionalCalendarProjectionService projection,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var window = ResolveWindow(fromUtc, toUtc);
        if (!window.IsValid)
        {
            return Results.BadRequest(new { message = window.Error });
        }

        if (organizationId is not null && !access.CanReadOrganization(httpContext.User, organizationId.Value))
        {
            return Results.Forbid();
        }

        var rows = await LoadWindowAsync(db, window.FromUtc, window.ToUtc, organizationId, cancellationToken);
        var visible = rows
            .Select(x => projection.Project(x, httpContext.User))
            .Where(x => x is not null)
            .Cast<CalendarEventProjection>()
            .ToArray();

        return Results.Ok(new
        {
            fromUtc = window.FromUtc,
            toUtc = window.ToUtc,
            institutionalTimeZone = "America/Santiago",
            events = visible
        });
    }

    private static async Task<IResult> ExportIcsAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? organizationId,
        HttpContext httpContext,
        CalendarDbContext db,
        PmgmDbContext auditDb,
        IInstitutionalCalendarProjectionService projection,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var window = ResolveWindow(fromUtc, toUtc);
        if (!window.IsValid)
        {
            return Results.BadRequest(new { message = window.Error });
        }

        if (organizationId is not null && !access.CanReadOrganization(httpContext.User, organizationId.Value))
        {
            return Results.Forbid();
        }

        var rows = await LoadWindowAsync(db, window.FromUtc, window.ToUtc, organizationId, cancellationToken);
        var visible = rows
            .Select(x => projection.Project(x, httpContext.User))
            .Where(x => x is not null)
            .Cast<CalendarEventProjection>()
            .ToArray();

        audit.Add(httpContext, "calendar.ics.exported", "InstitutionalCalendar", organizationId?.ToString() ?? "institutional",
            organizationId, AuditResults.Success,
            new { window.FromUtc, window.ToUtc, organizationId, eventCount = visible.Length });
        await auditDb.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.ContentDisposition = "attachment; filename=pmgm-calendar.ics";
        return Results.Text(BuildIcs(visible), "text/calendar; charset=utf-8", Encoding.UTF8);
    }

    private static async Task<IResult> CreateManualAsync(
        CreateManualCalendarEventRequest request,
        HttpContext httpContext,
        CalendarDbContext db,
        PmgmDbContext auditDb,
        IInstitutionalCalendarProjectionService projection,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!projection.CanManage(request.OrganizationId, httpContext.User))
        {
            return Results.Forbid();
        }

        var status = request.Status ?? CalendarCodes.Status.Draft;
        var validationError = ValidateCommon(
            request.Title,
            request.EventType,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.TimeZoneId,
            request.OrganizationId,
            request.ScopeType,
            request.Visibility,
            status);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var calendarEvent = new InstitutionalCalendarEvent
        {
            Title = request.Title.Trim(),
            EventType = request.EventType.Trim(),
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc,
            TimeZoneId = request.TimeZoneId,
            LocationDisplay = request.LocationDisplay?.Trim(),
            SpaceId = request.SpaceId,
            OrganizationId = request.OrganizationId,
            ScopeType = request.ScopeType,
            ScopeReference = request.ScopeReference?.Trim(),
            Visibility = request.Visibility,
            Status = status,
            SourceModule = "calendar",
            SourceEntityType = "manual",
            SourceEntityId = string.Empty,
            SourceControlled = false,
            OccupancyOnlyWhenRestricted = request.OccupancyOnlyWhenRestricted,
            ResponsibleSubject = request.ResponsibleSubject?.Trim()
        };
        calendarEvent.SourceEntityId = calendarEvent.Id.ToString("N");

        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync(cancellationToken);

        audit.Add(httpContext, "calendar.event.created", nameof(InstitutionalCalendarEvent), calendarEvent.Id.ToString(),
            calendarEvent.OrganizationId, AuditResults.Success,
            new { calendarEvent.EventType, calendarEvent.ScopeType, calendarEvent.Visibility, calendarEvent.Status, calendarEvent.SpaceId });
        await auditDb.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/calendar/{calendarEvent.Id}", projection.Project(calendarEvent, httpContext.User));
    }

    private static async Task<IResult> UpsertSourceAsync(
        UpsertSourceCalendarEventRequest request,
        HttpContext httpContext,
        CalendarDbContext db,
        PmgmDbContext auditDb,
        IInstitutionalCalendarProjectionService projection,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!projection.CanManage(request.OrganizationId, httpContext.User))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.SourceModule) ||
            string.IsNullOrWhiteSpace(request.SourceEntityType) ||
            string.IsNullOrWhiteSpace(request.SourceEntityId))
        {
            return Results.BadRequest(new { message = "Módulo, tipo e identificador de la entidad fuente son obligatorios." });
        }

        var validationError = ValidateCommon(
            request.Title,
            request.EventType,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.TimeZoneId,
            request.OrganizationId,
            request.ScopeType,
            request.Visibility,
            request.Status);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var sourceModule = request.SourceModule.Trim();
        var sourceEntityType = request.SourceEntityType.Trim();
        var sourceEntityId = request.SourceEntityId.Trim();
        var existing = await db.CalendarEvents.SingleOrDefaultAsync(
            x => x.SourceModule == sourceModule &&
                 x.SourceEntityType == sourceEntityType &&
                 x.SourceEntityId == sourceEntityId,
            cancellationToken);

        var created = existing is null;
        InstitutionalCalendarEvent calendarEvent;
        if (existing is null)
        {
            calendarEvent = new InstitutionalCalendarEvent
            {
                Title = request.Title.Trim(),
                EventType = request.EventType.Trim(),
                StartsAtUtc = request.StartsAtUtc,
                EndsAtUtc = request.EndsAtUtc,
                TimeZoneId = request.TimeZoneId,
                LocationDisplay = request.LocationDisplay?.Trim(),
                SpaceId = request.SpaceId,
                OrganizationId = request.OrganizationId,
                ScopeType = request.ScopeType,
                ScopeReference = request.ScopeReference?.Trim(),
                Visibility = request.Visibility,
                Status = request.Status,
                SourceModule = sourceModule,
                SourceEntityType = sourceEntityType,
                SourceEntityId = sourceEntityId,
                SourceControlled = true,
                OccupancyOnlyWhenRestricted = request.OccupancyOnlyWhenRestricted,
                ResponsibleSubject = request.ResponsibleSubject?.Trim()
            };
            db.CalendarEvents.Add(calendarEvent);
        }
        else
        {
            if (!projection.CanManage(existing, httpContext.User))
            {
                return Results.Forbid();
            }

            calendarEvent = existing;
            calendarEvent.Title = request.Title.Trim();
            calendarEvent.EventType = request.EventType.Trim();
            calendarEvent.StartsAtUtc = request.StartsAtUtc;
            calendarEvent.EndsAtUtc = request.EndsAtUtc;
            calendarEvent.TimeZoneId = request.TimeZoneId;
            calendarEvent.LocationDisplay = request.LocationDisplay?.Trim();
            calendarEvent.SpaceId = request.SpaceId;
            calendarEvent.OrganizationId = request.OrganizationId;
            calendarEvent.ScopeType = request.ScopeType;
            calendarEvent.ScopeReference = request.ScopeReference?.Trim();
            calendarEvent.Visibility = request.Visibility;
            calendarEvent.Status = request.Status;
            calendarEvent.OccupancyOnlyWhenRestricted = request.OccupancyOnlyWhenRestricted;
            calendarEvent.ResponsibleSubject = request.ResponsibleSubject?.Trim();
            calendarEvent.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        audit.Add(httpContext, created ? "calendar.source_event.created" : "calendar.source_event.updated",
            nameof(InstitutionalCalendarEvent), calendarEvent.Id.ToString(), calendarEvent.OrganizationId,
            AuditResults.Success,
            new
            {
                calendarEvent.EventType,
                calendarEvent.Status,
                calendarEvent.SourceModule,
                calendarEvent.SourceEntityType,
                calendarEvent.SourceEntityId,
                calendarEvent.SpaceId
            });
        await auditDb.SaveChangesAsync(cancellationToken);

        var result = projection.Project(calendarEvent, httpContext.User);
        return created
            ? Results.Created($"/api/calendar/{calendarEvent.Id}", result)
            : Results.Ok(result);
    }

    private static async Task<IResult> CancelManualAsync(
        Guid eventId,
        HttpContext httpContext,
        CalendarDbContext db,
        PmgmDbContext auditDb,
        IInstitutionalCalendarProjectionService projection,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var calendarEvent = await db.CalendarEvents.SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return Results.NotFound();
        }

        if (!projection.CanManage(calendarEvent, httpContext.User))
        {
            return Results.Forbid();
        }

        if (calendarEvent.SourceControlled)
        {
            return Results.Conflict(new { message = "El evento es controlado por su módulo de origen y debe cancelarse desde esa fuente." });
        }

        calendarEvent.Status = CalendarCodes.Status.Cancelled;
        calendarEvent.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        audit.Add(httpContext, "calendar.event.cancelled", nameof(InstitutionalCalendarEvent), calendarEvent.Id.ToString(),
            calendarEvent.OrganizationId, AuditResults.Success,
            new { calendarEvent.EventType, calendarEvent.SpaceId });
        await auditDb.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<List<InstitutionalCalendarEvent>> LoadWindowAsync(
        CalendarDbContext db,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var query = db.CalendarEvents.AsNoTracking()
            .Where(x => x.EndsAtUtc > fromUtc && x.StartsAtUtc < toUtc);

        if (organizationId is not null)
        {
            query = query.Where(x => x.OrganizationId == organizationId || x.OrganizationId == null);
        }

        return await query
            .OrderBy(x => x.StartsAtUtc)
            .ThenBy(x => x.EndsAtUtc)
            .Take(2000)
            .ToListAsync(cancellationToken);
    }

    private static CalendarWindow ResolveWindow(DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        var now = DateTimeOffset.UtcNow;
        var from = fromUtc ?? now.AddDays(-7);
        var to = toUtc ?? now.AddDays(90);
        if (to <= from)
        {
            return new CalendarWindow(false, from, to, "El fin del período debe ser posterior al inicio.");
        }

        if (to - from > TimeSpan.FromDays(366))
        {
            return new CalendarWindow(false, from, to, "La consulta de calendario no puede superar 366 días.");
        }

        return new CalendarWindow(true, from, to, null);
    }

    private static string? ValidateCommon(
        string title,
        string eventType,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        string timeZoneId,
        Guid? organizationId,
        string scopeType,
        string visibility,
        string status)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 500)
        {
            return "El título es obligatorio y no puede superar 500 caracteres.";
        }

        if (string.IsNullOrWhiteSpace(eventType) || eventType.Length > 120)
        {
            return "El tipo de evento es obligatorio y no puede superar 120 caracteres.";
        }

        if (endsAtUtc <= startsAtUtc)
        {
            return "La fecha/hora de término debe ser posterior al inicio.";
        }

        if (!string.Equals(timeZoneId, "America/Santiago", StringComparison.Ordinal))
        {
            return "La zona horaria institucional admitida en esta fase es America/Santiago.";
        }

        if (!CalendarCodes.ScopeType.IsValid(scopeType) ||
            !CalendarCodes.Visibility.IsValid(visibility) ||
            !CalendarCodes.Status.IsValid(status))
        {
            return "Ámbito, visibilidad o estado de calendario no válido.";
        }

        if ((scopeType == CalendarCodes.ScopeType.Lodge || visibility == CalendarCodes.Visibility.Lodge) &&
            organizationId is null)
        {
            return "Los eventos de Taller requieren organizationId.";
        }

        return null;
    }

    private static string BuildIcs(IEnumerable<CalendarEventProjection> events)
    {
        var builder = new StringBuilder();
        AppendIcsLine(builder, "BEGIN:VCALENDAR");
        AppendIcsLine(builder, "VERSION:2.0");
        AppendIcsLine(builder, "PRODID:-//Gran Logia Mixta de Chile//Proyecto Milenio//ES");
        AppendIcsLine(builder, "CALSCALE:GREGORIAN");
        AppendIcsLine(builder, "METHOD:PUBLISH");
        AppendIcsLine(builder, "X-WR-CALNAME:PMGM");
        AppendIcsLine(builder, "X-WR-TIMEZONE:America/Santiago");

        var now = DateTimeOffset.UtcNow;
        foreach (var calendarEvent in events)
        {
            AppendIcsLine(builder, "BEGIN:VEVENT");
            AppendIcsLine(builder, $"UID:{calendarEvent.Id:N}@pmgm");
            AppendIcsLine(builder, $"DTSTAMP:{FormatIcsUtc(now)}");
            AppendIcsLine(builder, $"DTSTART:{FormatIcsUtc(calendarEvent.StartsAtUtc)}");
            AppendIcsLine(builder, $"DTEND:{FormatIcsUtc(calendarEvent.EndsAtUtc)}");
            AppendIcsLine(builder, $"SUMMARY:{EscapeIcs(calendarEvent.Title)}");
            if (!string.IsNullOrWhiteSpace(calendarEvent.LocationDisplay))
            {
                AppendIcsLine(builder, $"LOCATION:{EscapeIcs(calendarEvent.LocationDisplay)}");
            }

            AppendIcsLine(builder, $"STATUS:{MapIcsStatus(calendarEvent.Status)}");
            if (calendarEvent.IsMasked)
            {
                AppendIcsLine(builder, "X-PMGM-MASKED:TRUE");
            }

            AppendIcsLine(builder, "END:VEVENT");
        }

        AppendIcsLine(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    private static string FormatIcsUtc(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string MapIcsStatus(string status)
        => status switch
        {
            CalendarCodes.Status.Cancelled => "CANCELLED",
            CalendarCodes.Status.Tentative or CalendarCodes.Status.Draft => "TENTATIVE",
            _ => "CONFIRMED"
        };

    private static string EscapeIcs(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);

    private static void AppendIcsLine(StringBuilder builder, string value)
        => builder.Append(value).Append("\r\n");
}

public sealed record CreateManualCalendarEventRequest(
    string Title,
    string EventType,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string TimeZoneId,
    string? LocationDisplay,
    Guid? SpaceId,
    Guid? OrganizationId,
    string ScopeType,
    string? ScopeReference,
    string Visibility,
    string? Status,
    bool OccupancyOnlyWhenRestricted,
    string? ResponsibleSubject);

public sealed record UpsertSourceCalendarEventRequest(
    string Title,
    string EventType,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string TimeZoneId,
    string? LocationDisplay,
    Guid? SpaceId,
    Guid? OrganizationId,
    string ScopeType,
    string? ScopeReference,
    string Visibility,
    string Status,
    bool OccupancyOnlyWhenRestricted,
    string? ResponsibleSubject,
    string SourceModule,
    string SourceEntityType,
    string SourceEntityId);

internal sealed record CalendarWindow(bool IsValid, DateTimeOffset FromUtc, DateTimeOffset ToUtc, string? Error);
