using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;

namespace PMGM.Api.Modules.Audit;

public static class AuditResults
{
    public const string Success = "success";
    public const string Rejected = "rejected";
    public const string Observed = "observed";
}

public static class AuditMetadataSanitizer
{
    private static readonly HashSet<string> ForbiddenKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "PersonId",
        "CandidatePersonId",
        "MemberId",
        "InstitutionalNumber",
        "Email",
        "FirstNames",
        "LastNames",
        "DisplayName",
        "Grade",
        "Degree",
        "MembershipType",
        "OfficeType",
        "EventType",
        "Notes",
        "Content",
        "ExcuseReason"
    };

    public static string? Serialize(object? metadata)
    {
        if (metadata is null) return null;
        var node = JsonSerializer.SerializeToNode(metadata);
        Sanitize(node);
        return node?.ToJsonString();
    }

    private static void Sanitize(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToArray())
            {
                if (ForbiddenKeys.Contains(key))
                {
                    obj.Remove(key);
                    continue;
                }
                Sanitize(obj[key]);
            }
            return;
        }

        if (node is JsonArray array)
        {
            foreach (var child in array) Sanitize(child);
        }
    }
}

public static class AuditEventFactory
{
    public static AuditEvent Create(
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

        return new AuditEvent
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OrganizationId = organizationId,
            ActorSubject = subject,
            ActorDisplayName = displayName,
            Result = result,
            CorrelationId = correlationId,
            MetadataJson = AuditMetadataSanitizer.Serialize(metadata)
        };
    }
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
        => db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            action,
            entityType,
            entityId,
            organizationId,
            result,
            metadata));
}
