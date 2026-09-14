using PMGM.Api.Modules.Notifications;

namespace PMGM.Api.Modules.Notifications.Entities;

public sealed class NotificationTemplate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; set; }
    public int Version { get; set; } = 1;
    public required string Name { get; set; }
    public string? SubjectTemplate { get; set; }
    public required string BodyTemplate { get; set; }
    public string AllowedVariablesJson { get; set; } = "[]";
    public string Sensitivity { get; set; } = NotificationCodes.Classification.Internal;
    public string Status { get; set; } = NotificationCodes.TemplateStatus.Active;
    public DateTimeOffset EffectiveFromUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class NotificationMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid NotificationTemplateId { get; set; }
    public NotificationTemplate NotificationTemplate { get; set; } = null!;
    public required string TypeCode { get; set; }
    public required string RecipientSubject { get; set; }
    public string? RecipientEmail { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public string? ActionUrl { get; set; }
    public required string Classification { get; set; }
    public bool Mandatory { get; set; }
    public bool ActionRequired { get; set; }
    public string? ActionStatus { get; set; }
    public string? RelatedResourceType { get; set; }
    public string? RelatedResourceId { get; set; }
    public required string IdempotencyKey { get; set; }
    public string? SourceEventId { get; set; }
    public required string CorrelationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAtUtc { get; set; }
    public ICollection<NotificationDelivery> Deliveries { get; set; } = new List<NotificationDelivery>();
}

public sealed class NotificationDelivery
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid NotificationMessageId { get; set; }
    public NotificationMessage NotificationMessage { get; set; } = null!;
    public required string Channel { get; set; }
    public required string Status { get; set; }
    public string? Provider { get; set; }
    public DateTimeOffset ScheduledAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<NotificationDeliveryAttempt> Attempts { get; set; } = new List<NotificationDeliveryAttempt>();
}

public sealed class NotificationDeliveryAttempt
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid NotificationDeliveryId { get; set; }
    public NotificationDelivery NotificationDelivery { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public required string Status { get; set; }
    public string? Provider { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset AttemptedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAtUtc { get; set; }
}
