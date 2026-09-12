using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;

namespace PMGM.Api.Modules.InstitutionalCalendar;

public sealed class CalendarSourceProjectionInterceptor(
    CalendarDbContext calendarDb,
    ILogger<CalendarSourceProjectionInterceptor> logger) : SaveChangesInterceptor
{
    private static readonly TimeZoneInfo Santiago = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
    private readonly ConcurrentDictionary<DbContext, List<SourceSnapshot>> _pending = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await FlushAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
        {
            _pending.TryRemove(eventData.Context, out _);
        }
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _pending.TryRemove(eventData.Context, out _);
        }
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null || context is CalendarDbContext)
        {
            return;
        }

        var snapshots = new List<SourceSnapshot>();
        if (context is PmgmDbContext)
        {
            foreach (var entry in context.ChangeTracker.Entries<CeremonyRequest>()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified))
            {
                var ceremony = entry.Entity;
                if (ceremony.ProposedDate is null)
                {
                    snapshots.Add(SourceSnapshot.Cancel(
                        "ceremonies",
                        "ceremony-request",
                        ceremony.Id.ToString("N"),
                        ceremony.OrganizationId));
                    continue;
                }

                var (startUtc, endUtc) = ToInstitutionalDay(ceremony.ProposedDate.Value);
                snapshots.Add(new SourceSnapshot(
                    "ceremonies",
                    "ceremony-request",
                    ceremony.Id.ToString("N"),
                    $"Ceremonia — {ceremony.CeremonyType}",
                    "ceremony_day",
                    startUtc,
                    endUtc,
                    ceremony.OrganizationId,
                    CalendarCodes.Visibility.Restricted,
                    MapCeremonyStatus(ceremony.Status),
                    false,
                    false));
            }
        }
        else if (context is LodgeManagementDbContext)
        {
            foreach (var entry in context.ChangeTracker.Entries<LodgeMeeting>()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified))
            {
                var meeting = entry.Entity;
                var (startUtc, endUtc) = ToInstitutionalDay(meeting.MeetingDate);
                snapshots.Add(new SourceSnapshot(
                    "lodge-management",
                    "lodge-meeting",
                    meeting.Id.ToString("N"),
                    "Tenida de Taller",
                    "lodge_meeting_day",
                    startUtc,
                    endUtc,
                    meeting.OrganizationId,
                    CalendarCodes.Visibility.Lodge,
                    MapMeetingStatus(meeting.Status),
                    false,
                    false));
            }

            foreach (var entry in context.ChangeTracker.Entries<LodgeInstructionSession>()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified))
            {
                var instruction = entry.Entity;
                var (startUtc, endUtc) = ToInstitutionalDay(instruction.InstructionDate);
                snapshots.Add(new SourceSnapshot(
                    "lodge-management",
                    "lodge-instruction",
                    instruction.Id.ToString("N"),
                    "Docencia de Taller",
                    "lodge_instruction_day",
                    startUtc,
                    endUtc,
                    instruction.OrganizationId,
                    CalendarCodes.Visibility.Lodge,
                    instruction.Status == LodgeManagementCodes.InstructionStatus.Cancelled
                        ? CalendarCodes.Status.Cancelled
                        : CalendarCodes.Status.Completed,
                    false,
                    false));
            }
        }

        if (snapshots.Count > 0)
        {
            _pending[context] = snapshots;
        }
    }

    private async Task FlushAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !_pending.TryRemove(context, out var snapshots) || snapshots.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var snapshot in snapshots)
            {
                var existing = await calendarDb.CalendarEvents.SingleOrDefaultAsync(
                    x => x.SourceModule == snapshot.SourceModule &&
                         x.SourceEntityType == snapshot.SourceEntityType &&
                         x.SourceEntityId == snapshot.SourceEntityId,
                    cancellationToken);

                if (snapshot.CancelOnly)
                {
                    if (existing is not null)
                    {
                        existing.Status = CalendarCodes.Status.Cancelled;
                        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    }
                    continue;
                }

                if (existing is null)
                {
                    calendarDb.CalendarEvents.Add(new InstitutionalCalendarEvent
                    {
                        Title = snapshot.Title,
                        EventType = snapshot.EventType,
                        StartsAtUtc = snapshot.StartsAtUtc!.Value,
                        EndsAtUtc = snapshot.EndsAtUtc!.Value,
                        OrganizationId = snapshot.OrganizationId,
                        ScopeType = CalendarCodes.ScopeType.Lodge,
                        Visibility = snapshot.Visibility,
                        Status = snapshot.Status,
                        SourceModule = snapshot.SourceModule,
                        SourceEntityType = snapshot.SourceEntityType,
                        SourceEntityId = snapshot.SourceEntityId,
                        SourceControlled = true,
                        OccupancyOnlyWhenRestricted = snapshot.OccupancyOnlyWhenRestricted
                    });
                }
                else
                {
                    existing.Title = snapshot.Title;
                    existing.EventType = snapshot.EventType;
                    existing.StartsAtUtc = snapshot.StartsAtUtc!.Value;
                    existing.EndsAtUtc = snapshot.EndsAtUtc!.Value;
                    existing.OrganizationId = snapshot.OrganizationId;
                    existing.ScopeType = CalendarCodes.ScopeType.Lodge;
                    existing.Visibility = snapshot.Visibility;
                    existing.Status = snapshot.Status;
                    existing.SourceControlled = true;
                    existing.OccupancyOnlyWhenRestricted = snapshot.OccupancyOnlyWhenRestricted;
                    existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }
            }

            await calendarDb.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            calendarDb.ChangeTracker.Clear();
            logger.LogWarning(ex,
                "No fue posible proyectar automáticamente {Count} cambios al calendario. La reconciliación posterior reparará el estado.",
                snapshots.Count);
        }
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) ToInstitutionalDay(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, Santiago), TimeSpan.Zero),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, Santiago), TimeSpan.Zero));
    }

    private static string MapCeremonyStatus(string status)
        => status switch
        {
            CeremonyCodes.RequestStatus.Draft => CalendarCodes.Status.Draft,
            CeremonyCodes.RequestStatus.Authorized => CalendarCodes.Status.Confirmed,
            CeremonyCodes.RequestStatus.Rejected => CalendarCodes.Status.Cancelled,
            _ => CalendarCodes.Status.Tentative
        };

    private static string MapMeetingStatus(string status)
        => status switch
        {
            LodgeManagementCodes.MeetingStatus.Cancelled => CalendarCodes.Status.Cancelled,
            LodgeManagementCodes.MeetingStatus.Closed => CalendarCodes.Status.Completed,
            LodgeManagementCodes.MeetingStatus.Scheduled or LodgeManagementCodes.MeetingStatus.Open => CalendarCodes.Status.Confirmed,
            _ => CalendarCodes.Status.Tentative
        };

    private sealed record SourceSnapshot(
        string SourceModule,
        string SourceEntityType,
        string SourceEntityId,
        string Title,
        string EventType,
        DateTimeOffset? StartsAtUtc,
        DateTimeOffset? EndsAtUtc,
        Guid OrganizationId,
        string Visibility,
        string Status,
        bool OccupancyOnlyWhenRestricted,
        bool CancelOnly)
    {
        public static SourceSnapshot Cancel(
            string sourceModule,
            string sourceEntityType,
            string sourceEntityId,
            Guid organizationId)
            => new(
                sourceModule,
                sourceEntityType,
                sourceEntityId,
                string.Empty,
                string.Empty,
                null,
                null,
                organizationId,
                CalendarCodes.Visibility.Restricted,
                CalendarCodes.Status.Cancelled,
                false,
                true);
    }
}
