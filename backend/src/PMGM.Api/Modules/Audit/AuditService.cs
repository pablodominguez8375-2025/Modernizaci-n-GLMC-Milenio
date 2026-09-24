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
        "ExcuseReason",
        "Title",
        "OriginalFileName",
        "ObjectKey",
        "Sha256",
        "ScanReference"
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

        var correlationId = ResolveCorrelationId(
            httpContext.TraceIdentifier,
            httpContext.Request.Headers["X-Correlation-ID"].ToString());

        return new AuditEvent
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OrganizationId = organizationId,
            ActorSubject = subject,
            ActorDisplayName = displayName,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Menu = ResolveMenu(action),
            Submenu = action.Split('.', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "General",
            Summary = action.Replace('.', ' '),
            Result = result,
            CorrelationId = correlationId,
            MetadataJson = AuditMetadataSanitizer.Serialize(metadata)
        };
    }

    internal static string ResolveCorrelationId(string? fallback, string? supplied)
    {
        if (string.IsNullOrEmpty(supplied) || supplied.Length > 128)
            return string.IsNullOrWhiteSpace(fallback) ? "server-generated" : fallback;

        foreach (var character in supplied)
        {
            var asciiAlphaNumeric = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
            if (!asciiAlphaNumeric && character is not '-' and not '_' and not '.' and not ':')
                return string.IsNullOrWhiteSpace(fallback) ? "server-generated" : fallback;
        }

        return supplied;
    }

    private static string ResolveMenu(string action)
        => action.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() switch
        {
            "system" => "Sistema", "admission" => "Insinuados", "membership" => "Miembros",
            "privacy" => "Privacidad", "calendar" => "Calendario", "lodge" => "Gestión Logial",
            _ => "Institucional"
        };
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
