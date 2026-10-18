using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.InstitutionalCalendar;
using PMGM.Api.Modules.InstitutionalCalendar.Entities;

namespace PMGM.Api.Modules.Notifications;

/// <summary>
/// Fail-closed boundary for calendar-related announcements. No grade or membership
/// is ever accepted from the caller. Generic notifications remain unchanged.
/// This phase intentionally prohibits email for calendar notices until the delivery
/// address can be bound to a verified recipient identity.
/// </summary>
public static class CalendarNotificationRecipientGuard
{
    public static bool IsProtected(string? typeCode, string? relatedResourceType,
        string? actionUrl, string? templateCode = null)
    {
        static bool ProtectedCode(string? value) => value is not null &&
            (value.StartsWith("calendar.", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("agenda.", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("lodge.meeting.", StringComparison.OrdinalIgnoreCase) ||
             value.StartsWith("lodge.instruction.", StringComparison.OrdinalIgnoreCase));

        var resource = relatedResourceType?.Trim().Replace('-', '_').ToLowerInvariant();
        var protectedResource = resource is "calendar" or "calendar_event" or
            "lodge_meeting" or "lodge_instruction";
        var path = actionUrl?.Split('?', '#')[0].TrimEnd('/');
        var protectedRoute = path is not null && (
            path.Equals("/agenda", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/agenda/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/calendar", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/calendar/", StringComparison.OrdinalIgnoreCase));
        return ProtectedCode(typeCode) || ProtectedCode(templateCode) || protectedResource || protectedRoute;
    }

    public static bool CanDeliver(CalendarGradeReader reader,
        InstitutionalCalendarEvent? calendarEvent, IReadOnlyCollection<string> channels)
    {
        // Restricted ceremonies may expose private details despite a matching grade.
        if (calendarEvent is null || calendarEvent.ScopeType != CalendarCodes.ScopeType.Lodge ||
            calendarEvent.Visibility != CalendarCodes.Visibility.Lodge ||
            calendarEvent.OrganizationId is not Guid organizationId ||
            !CalendarCodes.Grade.IsValid(calendarEvent.Grade) ||
            !reader.ActiveLodges.Contains(organizationId))
            return false;

        return channels.Count > 0 && channels.All(x =>
                string.Equals(x, NotificationCodes.Channel.Internal, StringComparison.OrdinalIgnoreCase)) &&
            reader.CanSee(calendarEvent);
    }

    public static async Task<bool> HasUniqueRecipientLinkAsync(
        PmgmDbContext db, string? subject, string? issuer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer))
            return false;

        var connection = db.Database.GetDbConnection();
        var close = connection.State != ConnectionState.Open;
        if (close) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*), COUNT(*) FILTER (WHERE "Issuer" = @issuer)
                FROM core.member_identity_links
                WHERE "Subject" = @subject AND "RevokedAtUtc" IS NULL;
                """;
            var recipient = command.CreateParameter();
            recipient.ParameterName = "@subject";
            recipient.Value = subject;
            command.Parameters.Add(recipient);
            var expectedIssuer = command.CreateParameter();
            expectedIssuer.ParameterName = "@issuer";
            expectedIssuer.Value = issuer;
            command.Parameters.Add(expectedIssuer);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return false;
            return reader.GetInt64(0) == 1 && reader.GetInt64(1) == 1;
        }
        finally
        {
            if (close) await connection.CloseAsync();
        }
    }

    public static async Task<bool> AuthorizeQueueAsync(
        QueueNotificationRequest request, ClaimsPrincipal sender,
        PmgmDbContext institutionalDb, CalendarDbContext calendarDb,
        ICalendarGradeVisibilityService gradeVisibility, CancellationToken ct)
    {
        if (!IsProtected(request.TypeCode, request.RelatedResourceType,
                request.ActionUrl, request.TemplateCode))
            return true;

        // Reject ambiguous/missing event references and unverified outbound email.
        if (!string.Equals(request.RelatedResourceType, "calendar_event",
                StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(request.RelatedResourceId, out var eventId) ||
            eventId == Guid.Empty || request.Channels is null ||
            !string.IsNullOrWhiteSpace(request.RecipientEmail) ||
            (request.SourceEventId is not null &&
             (!Guid.TryParse(request.SourceEventId, out var sourceId) || sourceId != eventId)) ||
            request.Channels.Any(x => !string.Equals(x, NotificationCodes.Channel.Internal,
                StringComparison.OrdinalIgnoreCase)))
            return false;

        var issuer = sender.FindFirstValue("iss");
        if (!await HasUniqueRecipientLinkAsync(institutionalDb, request.RecipientSubject, issuer, ct))
            return false;

        // Synthesized identity has NO administrative/organization role claims.
        // Membership and officer assignments are resolved from the database.
        var recipient = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("sub", request.RecipientSubject), new Claim("iss", issuer!) },
            "calendar-recipient"));
        var reader = await gradeVisibility.ForUserAsync(recipient, ct);
        var calendarEvent = await calendarDb.CalendarEvents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == eventId, ct);
        return CanDeliver(reader, calendarEvent, request.Channels);
    }
}
