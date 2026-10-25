using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.GrandSecretariat.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Notifications;

namespace PMGM.Api.Modules.GrandSecretariat;

/// <summary>
/// Aviso interno por una NUEVA solicitud; solo llega a subjects institucionales
/// individualmente verificados y con perfil de Gran Secretaría de alcance Orden.
/// El expediente, auditoría y todos los avisos comparten UNA transacción.
/// </summary>
public static class SpaceRequestCreatedNotices
{
    public const string TemplateCode = "grand_secretariat.space_request.created";
    public const string RecipientProfileCode = "grand-secretariat";

    public static string[] SelectAssignedSubjects(DynamicAccessCatalog catalog, DateOnly today) =>
        catalog.Assignments
            .Where(a => a.IsActive && a.OrganizationId is null &&
                        a.EffectiveFrom <= today &&
                        (a.EffectiveTo is null || a.EffectiveTo >= today))
            .Join(catalog.Profiles.Where(p =>
                    p.IsActive && !p.IsSystem && p.Scope == "order" &&
                    string.Equals(p.Code, RecipientProfileCode, StringComparison.OrdinalIgnoreCase) &&
                    p.MenuCodes.Contains("order") &&
                    p.Grants.Any(g => g.ViewCode == "secretariat" &&
                        g.Actions.Contains("view"))),
                a => a.ProfileCode, p => p.Code, (a, _) => a.Subject)
            .Where(subject => !string.IsNullOrWhiteSpace(subject) && subject.Length <= 320)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static async Task<int> QueueInsideCreateAsync(
        GrandSecretariatDbContext requestDb, InstitutionalSpaceRequest request,
        HttpContext http, CancellationToken ct)
    {
        var tx = requestDb.Database.CurrentTransaction
            ?? throw new InvalidOperationException("La notificación requiere una transacción activa.");

        // Los tres contextos trabajan sobre la MISMA conexión y transacción.
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(requestDb.Database.GetDbConnection()).Options;
        await using var institutions = new PmgmDbContext(options);
        await institutions.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);

        var catalog = await DynamicAccessEndpoints.LoadAsync(institutions, ct);
        var today = DynamicAccessEndpoints.Today();
        var candidates = SelectAssignedSubjects(catalog, today);
        // El Subject no identifica por sí solo un usuario si dos proveedores
        // OIDC emiten el mismo texto. Exigir el issuer de la sesión de origen.
        var issuer = http.User.FindFirstValue("iss");
        if (candidates.Length == 0 || string.IsNullOrWhiteSpace(issuer))
            throw new SpaceRequestRecipientsUnavailableException();

        // No se permite dirigir avisos a un cargo, un correo, un OrganizationId,
        // a múltiples enlaces OIDC para un mismo subject ni a un miembro inactivo.
        var matches = new List<(string Subject, Guid MemberId, string Issuer)>();
        var connection = requestDb.Database.GetDbConnection();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx.GetDbTransaction();
            cmd.CommandText = """
                SELECT "Subject", "MemberId", "Issuer"
                FROM core.member_identity_links
                WHERE "RevokedAtUtc" IS NULL AND "Subject" = ANY(@subjects);
                """;
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = "@subjects";
            parameter.Value = candidates;
            cmd.Parameters.Add(parameter);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                matches.Add((reader.GetString(0), reader.GetGuid(1), reader.GetString(2)));
        }

        var active = new List<string>();
        foreach (var group in matches.GroupBy(x => x.Subject, StringComparer.Ordinal))
        {
            if (group.Count() != 1 || group.First().MemberId == Guid.Empty ||
                !string.Equals(group.First().Issuer, issuer, StringComparison.Ordinal))
                continue;
            var memberId = group.First().MemberId;
            var membership = await institutions.Memberships.AsNoTracking().AnyAsync(m =>
                m.MemberId == memberId &&
                m.Status == MembershipCodes.MembershipStatus.Active &&
                (m.StartDate == null || m.StartDate <= today) &&
                (m.EndDate == null || m.EndDate >= today), ct);
            if (!membership) continue;
            var latestStatus = await institutions.InstitutionalStatusEvents.AsNoTracking()
                .Where(e => e.MemberId == memberId && e.EffectiveDate <= today &&
                    (e.EventType == MembershipCodes.InstitutionalStatus.Active ||
                     e.EventType == MembershipCodes.InstitutionalStatus.Reinstated ||
                     e.EventType == MembershipCodes.InstitutionalStatus.Inactive ||
                     e.EventType == MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal ||
                     e.EventType == MembershipCodes.InstitutionalStatus.ForcedWithdrawal ||
                     e.EventType == MembershipCodes.InstitutionalStatus.Deceased))
                .OrderByDescending(e => e.EffectiveDate)
                .ThenByDescending(e => e.RecordedAtUtc)
                .Select(e => e.EventType).FirstOrDefaultAsync(ct);
            if (latestStatus is not null && latestStatus is not
                (MembershipCodes.InstitutionalStatus.Active or MembershipCodes.InstitutionalStatus.Reinstated))
                continue;
            active.Add(group.Key);
        }

        if (active.Count == 0)
            throw new SpaceRequestRecipientsUnavailableException();

        var notificationsOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(connection).Options;
        await using var notifications = new NotificationDbContext(notificationsOptions);
        await notifications.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
        var service = new InstitutionalNotificationService(notifications);

        foreach (var subject in active.OrderBy(x => x, StringComparer.Ordinal))
        {
            var fingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(subject))).ToLowerInvariant();
            await service.QueueAsync(new QueueNotificationCommand(
                TemplateCode: TemplateCode,
                TemplateVersion: 1,
                TypeCode: TemplateCode,
                RecipientSubject: subject,
                RecipientEmail: null,
                Channels: [NotificationCodes.Channel.Internal],
                Variables: new Dictionary<string, string?>(),
                IdempotencyKey: "space-request:" + request.Id.ToString("D") +
                                ":created:" + fingerprint,
                CorrelationId: http.TraceIdentifier,
                SourceEventId: request.Id.ToString("D"),
                ActionUrl: "/gran-secretaria",
                Mandatory: true,
                ScheduledAtUtc: null,
                RelatedResourceType: "space_request",
                RelatedResourceId: request.Id.ToString("D")
            ), ct);
        }

        return active.Count;
    }
}

public sealed class SpaceRequestRecipientsUnavailableException : Exception
{
    public SpaceRequestRecipientsUnavailableException()
        : base("No hay destinatarios de Gran Secretaría con identidad institucional verificada.") { }
}
