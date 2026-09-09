using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;
using PMGM.Api.Modules.LodgeManagement;

namespace PMGM.Api.Modules.InstitutionalCalendar;

public interface IInstitutionalCalendarSourceSyncService
{
    Task<CalendarSourceSyncResult> ReconcileAsync(CancellationToken cancellationToken);
}

public sealed class InstitutionalCalendarSourceSyncService(
    PmgmDbContext institutionalDb,
    GrandSecretariatDbContext grandSecretariatDb,
    LodgeManagementDbContext lodgeDb,
    CalendarDbContext calendarDb) : IInstitutionalCalendarSourceSyncService
{
    private static readonly TimeZoneInfo Santiago = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");

    public async Task<CalendarSourceSyncResult> ReconcileAsync(CancellationToken cancellationToken)
    {
        var created = 0;
        var updated = 0;
        var skipped = 0;

        var reservations = await grandSecretariatDb.SpaceReservations
            .AsNoTracking()
            .Include(x => x.Space)
            .ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            var status = reservation.Status == GrandSecretariatCodes.ReservationStatus.Cancelled
                ? CalendarCodes.Status.Cancelled
                : CalendarCodes.Status.Confirmed;

            var result = await UpsertAsync(
                sourceModule: "grand-secretariat",
                sourceEntityType: "space-reservation",
                sourceEntityId: reservation.Id.ToString("N"),
                title: reservation.Purpose,
                eventType: reservation.CeremonyRequestId is null ? "space_reservation" : "ceremony_reservation",
                startsAtUtc: reservation.StartsAtUtc,
                endsAtUtc: reservation.EndsAtUtc,
                organizationId: reservation.OrganizationId,
                spaceId: reservation.SpaceId,
                locationDisplay: BuildSpaceDisplay(reservation.Space.Name, reservation.Space.Location),
                visibility: CalendarCodes.Visibility.Restricted,
                status: status,
                occupancyOnlyWhenRestricted: true,
                cancellationToken: cancellationToken);
            Count(result, ref created, ref updated);
        }

        var ceremonies = await institutionalDb.CeremonyRequests.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var ceremony in ceremonies)
        {
            var sourceEntityId = ceremony.Id.ToString("N");
            if (ceremony.ProposedDate is null)
            {
                var cancelledExisting = await CancelExistingProjectionAsync(
                    "ceremonies", "ceremony-request", sourceEntityId, cancellationToken);
                if (cancelledExisting) updated++;
                else skipped++;
                continue;
            }

            var (startUtc, endUtc) = ToInstitutionalDay(ceremony.ProposedDate.Value);
            var result = await UpsertAsync(
                sourceModule: "ceremonies",
                sourceEntityType: "ceremony-request",
                sourceEntityId: sourceEntityId,
                title: $"Ceremonia — {ceremony.CeremonyType}",
                eventType: "ceremony_day",
                startsAtUtc: startUtc,
                endsAtUtc: endUtc,
                organizationId: ceremony.OrganizationId,
                spaceId: null,
                locationDisplay: null,
                visibility: CalendarCodes.Visibility.Restricted,
                status: MapCeremonyStatus(ceremony.Status),
                occupancyOnlyWhenRestricted: false,
                cancellationToken: cancellationToken);
            Count(result, ref created, ref updated);
        }

        var meetings = await lodgeDb.LodgeMeetings.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var meeting in meetings)
        {
            var (startUtc, endUtc) = ToInstitutionalDay(meeting.MeetingDate);
            var result = await UpsertAsync(
                sourceModule: "lodge-management",
                sourceEntityType: "lodge-meeting",
                sourceEntityId: meeting.Id.ToString("N"),
                title: "Tenida de Taller",
                eventType: "lodge_meeting_day",
                startsAtUtc: startUtc,
                endsAtUtc: endUtc,
                organizationId: meeting.OrganizationId,
                spaceId: null,
                locationDisplay: null,
                visibility: CalendarCodes.Visibility.Lodge,
                status: MapMeetingStatus(meeting.Status),
                occupancyOnlyWhenRestricted: false,
                cancellationToken: cancellationToken);
            Count(result, ref created, ref updated);
        }

        var instructions = await lodgeDb.LodgeInstructionSessions.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var instruction in instructions)
        {
            var (startUtc, endUtc) = ToInstitutionalDay(instruction.InstructionDate);
            var result = await UpsertAsync(
                sourceModule: "lodge-management",
                sourceEntityType: "lodge-instruction",
                sourceEntityId: instruction.Id.ToString("N"),
                title: "Docencia de Taller",
                eventType: "lodge_instruction_day",
                startsAtUtc: startUtc,
                endsAtUtc: endUtc,
                organizationId: instruction.OrganizationId,
                spaceId: null,
                locationDisplay: null,
                visibility: CalendarCodes.Visibility.Lodge,
                status: instruction.Status == LodgeManagementCodes.InstructionStatus.Cancelled
                    ? CalendarCodes.Status.Cancelled
                    : CalendarCodes.Status.Completed,
                occupancyOnlyWhenRestricted: false,
                cancellationToken: cancellationToken);
            Count(result, ref created, ref updated);
        }

        await calendarDb.SaveChangesAsync(cancellationToken);
        return new CalendarSourceSyncResult(created, updated, skipped,
            reservations.Count, ceremonies.Count, meetings.Count, instructions.Count);
    }

    private async Task<bool> UpsertAsync(
        string sourceModule,
        string sourceEntityType,
        string sourceEntityId,
        string title,
        string eventType,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        Guid organizationId,
        Guid? spaceId,
        string? locationDisplay,
        string visibility,
        string status,
        bool occupancyOnlyWhenRestricted,
        CancellationToken cancellationToken)
    {
        var existing = await calendarDb.CalendarEvents.SingleOrDefaultAsync(
            x => x.SourceModule == sourceModule &&
                 x.SourceEntityType == sourceEntityType &&
                 x.SourceEntityId == sourceEntityId,
            cancellationToken);

        if (existing is null)
        {
            calendarDb.CalendarEvents.Add(new InstitutionalCalendarEvent
            {
                Title = title,
                EventType = eventType,
                StartsAtUtc = startsAtUtc,
                EndsAtUtc = endsAtUtc,
                OrganizationId = organizationId,
                SpaceId = spaceId,
                LocationDisplay = locationDisplay,
                ScopeType = CalendarCodes.ScopeType.Lodge,
                Visibility = visibility,
                Status = status,
                SourceModule = sourceModule,
                SourceEntityType = sourceEntityType,
                SourceEntityId = sourceEntityId,
                SourceControlled = true,
                OccupancyOnlyWhenRestricted = occupancyOnlyWhenRestricted
            });
            return true;
        }

        existing.Title = title;
        existing.EventType = eventType;
        existing.StartsAtUtc = startsAtUtc;
        existing.EndsAtUtc = endsAtUtc;
        existing.OrganizationId = organizationId;
        existing.SpaceId = spaceId;
        existing.LocationDisplay = locationDisplay;
        existing.ScopeType = CalendarCodes.ScopeType.Lodge;
        existing.Visibility = visibility;
        existing.Status = status;
        existing.SourceControlled = true;
        existing.OccupancyOnlyWhenRestricted = occupancyOnlyWhenRestricted;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return false;
    }

    private async Task<bool> CancelExistingProjectionAsync(
        string sourceModule,
        string sourceEntityType,
        string sourceEntityId,
        CancellationToken cancellationToken)
    {
        var existing = await calendarDb.CalendarEvents.SingleOrDefaultAsync(
            x => x.SourceModule == sourceModule &&
                 x.SourceEntityType == sourceEntityType &&
                 x.SourceEntityId == sourceEntityId,
            cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.Status = CalendarCodes.Status.Cancelled;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) ToInstitutionalDay(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, Santiago);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, Santiago);
        return (new DateTimeOffset(startUtc, TimeSpan.Zero), new DateTimeOffset(endUtc, TimeSpan.Zero));
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

    private static string BuildSpaceDisplay(string name, string? location)
        => string.IsNullOrWhiteSpace(location) ? name : $"{name} — {location}";

    private static void Count(bool createdEvent, ref int created, ref int updated)
    {
        if (createdEvent) created++;
        else updated++;
    }
}

public sealed record CalendarSourceSyncResult(
    int Created,
    int Updated,
    int Skipped,
    int ReservationsScanned,
    int CeremoniesScanned,
    int MeetingsScanned,
    int InstructionSessionsScanned);
