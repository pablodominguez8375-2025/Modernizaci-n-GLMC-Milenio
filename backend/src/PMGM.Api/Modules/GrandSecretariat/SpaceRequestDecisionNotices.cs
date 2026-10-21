using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PMGM.Api.Data;
using PMGM.Api.Modules.GrandSecretariat.Entities;
using PMGM.Api.Modules.Notifications;

namespace PMGM.Api.Modules.GrandSecretariat;

/// <summary>
/// Internal-only notification to the stored requesting subject.
/// Writes through the *same* Npgsql connection and transaction as the
/// Grand Secretariat decision and reservation. No independent commit.
/// </summary>
public static class SpaceRequestDecisionNotices
{
    public const string TemplateCode = "grand_secretariat.space_request.resolved";

    public static async Task QueueInsideDecisionAsync(
        GrandSecretariatDbContext requestDb, InstitutionalSpaceRequest request,
        HttpContext http, CancellationToken cancellationToken)
    {
        if (request.Status is not ("approved" or "rejected"))
            return;

        // Historical pre-identity requests cannot be safely addressed by role,
        // email, lodge identifier or a guessed subject.
        if (string.IsNullOrWhiteSpace(request.CreatedBySubject) ||
            request.CreatedBySubject == "unknown")
            return;

        var transaction = requestDb.Database.CurrentTransaction
            ?? throw new InvalidOperationException("El aviso requiere la transacción de resolución de espacio.");

        // Reuse the exact connection/transaction: the message is rolled back if
        // either the request, audit or reservation cannot commit.
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(requestDb.Database.GetDbConnection())
            .Options;
        await using var notifications = new NotificationDbContext(options);
        await notifications.Database.UseTransactionAsync(
            transaction.GetDbTransaction(), cancellationToken);

        var statusLabel = request.Status == "approved" ? "aprobada" : "rechazada";
        var service = new InstitutionalNotificationService(notifications);
        await service.QueueAsync(new QueueNotificationCommand(
            TemplateCode: TemplateCode,
            TemplateVersion: 1,
            TypeCode: "grand_secretariat.space_request." + request.Status,
            RecipientSubject: request.CreatedBySubject,
            RecipientEmail: null,
            Channels: [NotificationCodes.Channel.Internal],
            Variables: new Dictionary<string, string?> { ["status"] = statusLabel },
            IdempotencyKey: "space-request:" + request.Id.ToString("D") + ":decision",
            CorrelationId: http.TraceIdentifier,
            SourceEventId: request.Id.ToString("D"),
            ActionUrl: "/secretaria",
            Mandatory: true,
            ScheduledAtUtc: null,
            RelatedResourceType: "space_request",
            RelatedResourceId: request.Id.ToString("D")
        ), cancellationToken);
    }
}
