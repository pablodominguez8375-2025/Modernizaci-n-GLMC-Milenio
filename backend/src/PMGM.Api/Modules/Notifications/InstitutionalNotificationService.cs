using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Notifications.Entities;

namespace PMGM.Api.Modules.Notifications;

public interface IInstitutionalNotificationService
{
    Task<QueueNotificationResult> QueueAsync(QueueNotificationCommand command, CancellationToken cancellationToken);
}

public sealed class InstitutionalNotificationService(NotificationDbContext db) : IInstitutionalNotificationService
{
    public async Task<QueueNotificationResult> QueueAsync(
        QueueNotificationCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new ArgumentException("La clave de idempotencia es obligatoria.", nameof(command));
        }

        var existing = await db.NotificationMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == command.IdempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return new QueueNotificationResult(existing.Id, false);
        }

        var now = DateTimeOffset.UtcNow;
        var templateQuery = db.NotificationTemplates
            .Where(x => x.Code == command.TemplateCode &&
                        x.Status == NotificationCodes.TemplateStatus.Active &&
                        x.EffectiveFromUtc <= now);

        var template = command.TemplateVersion is null
            ? await templateQuery.OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken)
            : await templateQuery.SingleOrDefaultAsync(x => x.Version == command.TemplateVersion.Value, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException("No existe una plantilla activa y vigente para la notificación solicitada.");
        }

        var channels = command.Channels.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (channels.Length == 0 || channels.Any(x => !NotificationCodes.Channel.IsValid(x)))
        {
            throw new ArgumentException("Debe indicar al menos un canal de notificación válido.", nameof(command));
        }

        if (channels.Contains(NotificationCodes.Channel.Email, StringComparer.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(command.RecipientEmail))
        {
            throw new ArgumentException("El correo del destinatario es obligatorio para el canal email.", nameof(command));
        }

        var allowedVariables = JsonSerializer.Deserialize<string[]>(template.AllowedVariablesJson) ?? [];
        var unexpected = command.Variables.Keys.Except(allowedVariables, StringComparer.Ordinal).ToArray();
        if (unexpected.Length > 0)
        {
            throw new ArgumentException($"La plantilla no autoriza estas variables: {string.Join(", ", unexpected)}.", nameof(command));
        }

        var subject = Render(template.SubjectTemplate ?? string.Empty, command.Variables);
        var body = Render(template.BodyTemplate, command.Variables);

        var message = new NotificationMessage
        {
            NotificationTemplateId = template.Id,
            TypeCode = command.TypeCode,
            RecipientSubject = command.RecipientSubject,
            RecipientEmail = command.RecipientEmail,
            Subject = subject,
            Body = body,
            ActionUrl = command.ActionUrl,
            Classification = template.Sensitivity,
            Mandatory = command.Mandatory,
            IdempotencyKey = command.IdempotencyKey,
            SourceEventId = command.SourceEventId,
            CorrelationId = command.CorrelationId
        };

        foreach (var channel in channels)
        {
            var internalChannel = string.Equals(channel, NotificationCodes.Channel.Internal, StringComparison.OrdinalIgnoreCase);
            var delivery = new NotificationDelivery
            {
                NotificationMessageId = message.Id,
                Channel = channel,
                Status = internalChannel ? NotificationCodes.DeliveryStatus.Delivered : NotificationCodes.DeliveryStatus.Queued,
                Provider = internalChannel ? "pmgm-internal" : null,
                ScheduledAtUtc = command.ScheduledAtUtc ?? now,
                DeliveredAtUtc = internalChannel ? now : null,
                AttemptCount = internalChannel ? 1 : 0
            };

            if (internalChannel)
            {
                delivery.Attempts.Add(new NotificationDeliveryAttempt
                {
                    NotificationDeliveryId = delivery.Id,
                    AttemptNumber = 1,
                    Status = NotificationCodes.DeliveryStatus.Delivered,
                    Provider = "pmgm-internal",
                    DeliveredAtUtc = now
                });
            }

            message.Deliveries.Add(delivery);
        }

        db.NotificationMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return new QueueNotificationResult(message.Id, true);
    }

    private static string Render(string template, IReadOnlyDictionary<string, string?> variables)
    {
        var output = template;
        foreach (var pair in variables)
        {
            output = output.Replace("{{" + pair.Key + "}}", pair.Value ?? string.Empty, StringComparison.Ordinal);
        }

        return output;
    }
}

public sealed record QueueNotificationCommand(
    string TemplateCode,
    int? TemplateVersion,
    string TypeCode,
    string RecipientSubject,
    string? RecipientEmail,
    IReadOnlyCollection<string> Channels,
    IReadOnlyDictionary<string, string?> Variables,
    string IdempotencyKey,
    string CorrelationId,
    string? SourceEventId,
    string? ActionUrl,
    bool Mandatory,
    DateTimeOffset? ScheduledAtUtc);

public sealed record QueueNotificationResult(Guid MessageId, bool Created);
