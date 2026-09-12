using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;

namespace PMGM.Api.Modules.InstitutionalCalendar;

public static class CalendarSourceEndpoints
{
    public static IEndpointRouteBuilder MapInstitutionalCalendarSourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/calendar/sources")
            .WithTags("Calendario institucional — fuentes")
            .RequireAuthorization();

        group.MapPost("/reconcile", ReconcileAsync);
        group.MapGet("/space-conflicts", GetSpaceConflictsAsync);
        return endpoints;
    }

    private static async Task<IResult> ReconcileAsync(
        HttpContext httpContext,
        PmgmDbContext auditDb,
        IInstitutionalCalendarSourceSyncService sync,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User))
        {
            return Results.Forbid();
        }

        var result = await sync.ReconcileAsync(cancellationToken);
        audit.Add(httpContext, "calendar.sources.reconciled", "InstitutionalCalendar", "sources", null,
            AuditResults.Success,
            new
            {
                result.Created,
                result.Updated,
                result.Skipped,
                result.ReservationsScanned,
                result.CeremoniesScanned,
                result.MeetingsScanned,
                result.InstructionSessionsScanned
            });
        await auditDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSpaceConflictsAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        Guid? spaceId,
        HttpContext httpContext,
        CalendarDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User))
        {
            return Results.Forbid();
        }

        var from = fromUtc ?? DateTimeOffset.UtcNow.AddDays(-1);
        var to = toUtc ?? DateTimeOffset.UtcNow.AddDays(90);
        if (to <= from || to - from > TimeSpan.FromDays(366))
        {
            return Results.BadRequest(new { message = "El período debe ser válido y no superar 366 días." });
        }

        var query = db.CalendarEvents.AsNoTracking()
            .Where(x => x.SpaceId != null &&
                        x.Status != CalendarCodes.Status.Cancelled &&
                        x.StartsAtUtc < to &&
                        x.EndsAtUtc > from);
        if (spaceId is not null)
        {
            query = query.Where(x => x.SpaceId == spaceId);
        }

        var events = await query
            .OrderBy(x => x.SpaceId)
            .ThenBy(x => x.StartsAtUtc)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var conflicts = new List<CalendarSpaceConflictDto>();
        foreach (var group in events.GroupBy(x => x.SpaceId!.Value))
        {
            var ordered = group.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.EndsAtUtc).ToArray();
            for (var i = 0; i < ordered.Length; i++)
            {
                for (var j = i + 1; j < ordered.Length && ordered[j].StartsAtUtc < ordered[i].EndsAtUtc; j++)
                {
                    var left = ordered[i];
                    var right = ordered[j];
                    if (IsSameLogicalSource(left, right))
                    {
                        continue;
                    }

                    conflicts.Add(new CalendarSpaceConflictDto(
                        group.Key,
                        left.Id,
                        right.Id,
                        Max(left.StartsAtUtc, right.StartsAtUtc),
                        Min(left.EndsAtUtc, right.EndsAtUtc),
                        ToSource(left),
                        ToSource(right)));
                }
            }
        }

        return Results.Ok(new
        {
            fromUtc = from,
            toUtc = to,
            spaceId,
            total = conflicts.Count,
            conflicts
        });
    }

    private static bool IsSameLogicalSource(InstitutionalCalendarEvent left, InstitutionalCalendarEvent right)
        => left.SourceModule == right.SourceModule &&
           left.SourceEntityType == right.SourceEntityType &&
           left.SourceEntityId == right.SourceEntityId;

    private static CalendarSourceReferenceDto ToSource(InstitutionalCalendarEvent calendarEvent)
        => new(calendarEvent.SourceModule, calendarEvent.SourceEntityType, calendarEvent.SourceEntityId,
            calendarEvent.OrganizationId, calendarEvent.Status);

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;
    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
}

public sealed record CalendarSpaceConflictDto(
    Guid SpaceId,
    Guid LeftEventId,
    Guid RightEventId,
    DateTimeOffset OverlapStartsAtUtc,
    DateTimeOffset OverlapEndsAtUtc,
    CalendarSourceReferenceDto LeftSource,
    CalendarSourceReferenceDto RightSource);

public sealed record CalendarSourceReferenceDto(
    string SourceModule,
    string SourceEntityType,
    string SourceEntityId,
    Guid? OrganizationId,
    string Status);
