using System.Security.Claims;
using System.Text.Json;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;

namespace PMGM.Api.Modules.Audit;

public static class AuditResults
{
    public const string Success = "success";
    public const string Rejected = "rejected";
    public const string Observed = "observed";
}

public interface IAuditService
{
    void Add(
        HttpContext httpContext,
        string action,
        string entityType,
        string entityId,
        Guid? organizationId,
        string result,
        object? metadata = null);
}

public sealed class AuditService(PmgmDbContext db) : IAuditService
{
    public void Add(
        HttpContext httpContext,
        string action,
        string entityType,
        string entityId,
        Guid? organizationId,
        string result,
        object? metadata = null)
    {
        var subject = httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var displayName = httpContext.User.Identity?.Name
            ?? httpContext.User.FindFirstValue("name")
            ?? httpContext.User.FindFirstValue(ClaimTypes.Name);

        var correlationId = httpContext.TraceIdentifier;
        if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var suppliedCorrelationId) &&
            !string.IsNullOrWhiteSpace(suppliedCorrelationId))
        {
            correlationId = suppliedCorrelationId.ToString();
        }

        db.AuditEvents.Add(new AuditEvent
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OrganizationId = organizationId,
            ActorSubject = subject,
            ActorDisplayName = displayName,
            Result = result,
            CorrelationId = correlationId,
            MetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata)
        });
    }
}
