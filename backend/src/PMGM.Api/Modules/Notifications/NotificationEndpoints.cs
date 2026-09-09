using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Notifications.Entities;

namespace PMGM.Api.Modules.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications")
            .WithTags("Notificaciones institucionales")
            .RequireAuthorization();

        group.MapPost("/templates", CreateTemplateAsync);
        group.MapPost("/queue", QueueAsync);
        group.MapGet("/me", GetMineAsync);
        group.MapPost("/{messageId:guid}/read", MarkReadAsync);
        group.MapGet("/operations", GetOperationsAsync);
        group.MapPost("/deliveries/{deliveryId:guid}/result", RecordDeliveryResultAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateTemplateAsync(
        CreateNotificationTemplateRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BodyTemplate))
        {
            return Results.BadRequest(new { message = "Código, nombre y cuerpo de plantilla son obligatorios." });
        }

        if (request.Version < 1 || !NotificationCodes.TemplateStatus.IsValid(request.Status) || !NotificationCodes.Classification.IsValid(request.Sensitivity))
        {
            return Results.BadRequest(new { message = "Versión, estado o clasificación de plantilla no válidos." });
        }

        var exists = await db.NotificationTemplates.AnyAsync(
            x => x.Code == request.Code && x.Version == request.Version,
            cancellationToken);
        if (exists)
        {
            return Results.Conflict(new { message = "Ya existe esa versión de la plantilla." });
        }

        var template = new NotificationTemplate
        {
            Code = request.Code.Trim(),
            Version = request.Version,
            Name = request.Name.Trim(),
            SubjectTemplate = request.SubjectTemplate,
            BodyTemplate = request.BodyTemplate,
            AllowedVariablesJson = JsonSerializer.Serialize(request.AllowedVariables.Distinct(StringComparer.Ordinal).ToArray()),
            Sensitivity = request.Sensitivity,
            Status = request.Status,
            EffectiveFromUtc = request.EffectiveFromUtc ?? DateTimeOffset.UtcNow
        };

        db.NotificationTemplates.Add(template);
        audit.Add(httpContext, "notification.template.created", nameof(NotificationTemplate), template.Id.ToString(), null,
            AuditResults.Success, new { template.Code, template.Version, template.Sensitivity });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/notifications/templates/{template.Id}", new
        {
            template.Id,
            template.Code,
            template.Version,
            template.Name,
            template.Sensitivity,
            template.Status,
            template.EffectiveFromUtc
        });
    }

    private static async Task<IResult> QueueAsync(
        QueueNotificationRequest request,
        HttpContext httpContext,
        IInstitutionalAccessService access,
        IInstitutionalNotificationService service,
        IAuditService audit,
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        var canQueue = access.CanManageGrandSecretariat(httpContext.User) ||
                       (access.HasOrderScope(httpContext.User) &&
                        access.HasRole(httpContext.User, InstitutionalRoles.PrivacyOfficer));
        if (!canQueue)
        {
            return Results.Forbid();
        }

        var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? httpContext.TraceIdentifier;

        try
        {
            var result = await service.QueueAsync(new QueueNotificationCommand(
                request.TemplateCode,
                request.TemplateVersion,
                request.TypeCode,
                request.RecipientSubject,
                request.RecipientEmail,
                request.Channels,
                request.Variables,
                request.IdempotencyKey,
                correlationId,
                request.SourceEventId,
                request.ActionUrl,
                request.Mandatory,
                request.ScheduledAtUtc), cancellationToken);

            audit.Add(httpContext, result.Created ? "notification.queued" : "notification.duplicate_ignored",
                nameof(NotificationMessage), result.MessageId.ToString(), null, AuditResults.Success,
                new { request.TypeCode, request.Channels, result.Created });
            await db.SaveChangesAsync(cancellationToken);

            return result.Created
                ? Results.Created($"/api/notifications/{result.MessageId}", result)
                : Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }

    private static async Task<IResult> GetMineAsync(
        bool unreadOnly,
        int? limit,
        HttpContext httpContext,
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        var subject = httpContext.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Results.Forbid();
        }

        var take = Math.Clamp(limit ?? 50, 1, 100);
        var query = db.NotificationMessages.AsNoTracking()
            .Where(x => x.RecipientSubject == subject &&
                        x.Deliveries.Any(d => d.Channel == NotificationCodes.Channel.Internal &&
                                              d.Status == NotificationCodes.DeliveryStatus.Delivered));

        if (unreadOnly)
        {
            query = query.Where(x => x.ReadAtUtc == null);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => new NotificationInboxDto(x.Id, x.TypeCode, x.Subject, x.Body, x.ActionUrl,
                x.Classification, x.Mandatory, x.CreatedAtUtc, x.ReadAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> MarkReadAsync(
        Guid messageId,
        HttpContext httpContext,
        PmgmDbContext db,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var subject = httpContext.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Results.Forbid();
        }

        var message = await db.NotificationMessages.SingleOrDefaultAsync(
            x => x.Id == messageId && x.RecipientSubject == subject &&
                 x.Deliveries.Any(d => d.Channel == NotificationCodes.Channel.Internal),
            cancellationToken);
        if (message is null)
        {
            return Results.NotFound();
        }

        message.ReadAtUtc ??= DateTimeOffset.UtcNow;
        audit.Add(httpContext, "notification.read", nameof(NotificationMessage), message.Id.ToString(), null,
            AuditResults.Success, new { message.TypeCode });
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetOperationsAsync(
        string? status,
        int? limit,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User))
        {
            return Results.Forbid();
        }

        if (!string.IsNullOrWhiteSpace(status) && !NotificationCodes.DeliveryStatus.IsValid(status))
        {
            return Results.BadRequest(new { message = "Estado de entrega no válido." });
        }

        var take = Math.Clamp(limit ?? 100, 1, 200);
        var query = db.NotificationDeliveries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).Take(take)
            .Select(x => new
            {
                x.Id,
                x.NotificationMessageId,
                x.Channel,
                x.Status,
                x.Provider,
                x.ScheduledAtUtc,
                x.DeliveredAtUtc,
                x.AttemptCount,
                x.LastErrorCode,
                TypeCode = x.NotificationMessage.TypeCode,
                Recipient = x.NotificationMessage.RecipientSubject,
                x.NotificationMessage.Classification,
                x.NotificationMessage.CorrelationId
            })
            .ToListAsync(cancellationToken);
        return Results.Ok(rows);
    }

    private static async Task<IResult> RecordDeliveryResultAsync(
        Guid deliveryId,
        RecordDeliveryResultRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User))
        {
            return Results.Forbid();
        }

        if (!NotificationCodes.DeliveryStatus.IsValid(request.Status) || request.Status == NotificationCodes.DeliveryStatus.Queued)
        {
            return Results.BadRequest(new { message = "El resultado de entrega indicado no es válido." });
        }

        var delivery = await db.NotificationDeliveries.SingleOrDefaultAsync(x => x.Id == deliveryId, cancellationToken);
        if (delivery is null)
        {
            return Results.NotFound();
        }

        delivery.AttemptCount++;
        delivery.Status = request.Status;
        delivery.Provider = request.Provider;
        delivery.LastErrorCode = request.ErrorCode;
        delivery.DeliveredAtUtc = request.Status == NotificationCodes.DeliveryStatus.Delivered ? DateTimeOffset.UtcNow : null;
        delivery.Attempts.Add(new NotificationDeliveryAttempt
        {
            NotificationDeliveryId = delivery.Id,
            AttemptNumber = delivery.AttemptCount,
            Status = request.Status,
            Provider = request.Provider,
            ErrorCode = request.ErrorCode,
            DeliveredAtUtc = delivery.DeliveredAtUtc
        });

        audit.Add(httpContext, "notification.delivery.result_recorded", nameof(NotificationDelivery), delivery.Id.ToString(), null,
            AuditResults.Success, new { delivery.Channel, delivery.Status, delivery.AttemptCount, request.ErrorCode });
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

public sealed record CreateNotificationTemplateRequest(
    string Code,
    int Version,
    string Name,
    string? SubjectTemplate,
    string BodyTemplate,
    IReadOnlyCollection<string> AllowedVariables,
    string Sensitivity,
    string Status,
    DateTimeOffset? EffectiveFromUtc);

public sealed record QueueNotificationRequest(
    string TemplateCode,
    int? TemplateVersion,
    string TypeCode,
    string RecipientSubject,
    string? RecipientEmail,
    IReadOnlyCollection<string> Channels,
    IReadOnlyDictionary<string, string?> Variables,
    string IdempotencyKey,
    string? SourceEventId,
    string? ActionUrl,
    bool Mandatory,
    DateTimeOffset? ScheduledAtUtc);

public sealed record RecordDeliveryResultRequest(string Status, string? Provider, string? ErrorCode);

public sealed record NotificationInboxDto(
    Guid Id,
    string TypeCode,
    string Subject,
    string Body,
    string? ActionUrl,
    string Classification,
    bool Mandatory,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);
