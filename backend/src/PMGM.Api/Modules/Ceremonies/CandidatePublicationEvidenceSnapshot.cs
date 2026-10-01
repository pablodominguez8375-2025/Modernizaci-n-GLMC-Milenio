using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

public sealed record CandidatePublicationFieldPolicy(
    string Code, Guid? VersionId, string[] VisibleFields, DateOnly? EffectiveFrom, string SourceReference);

public sealed record CandidatePublicationFrozenEvidence(
    int SchemaVersion, Guid? PhotoVersionId, CandidatePublicationFieldPolicy Policy);

/// <summary>Private publication evidence persisted atomically in the existing approval audit.</summary>
public static class CandidatePublicationEvidenceStore
{
    public const string FieldsCode = "system.publication.candidate.visible_fields";
    public const string DefaultFields = "Fotografía|Nombre completo|Taller";
    public const string ApprovalAction = "ceremony.candidate_publication.approved_by_grand_secretariat";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? NormalizeFields(string? value)
    {
        var parts = (value ?? "").Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var approved = DefaultFields.Split('|');
        return parts.Length == approved.Length &&
               parts.Distinct(StringComparer.OrdinalIgnoreCase).Count() == approved.Length &&
               parts.All(part => approved.Contains(part, StringComparer.OrdinalIgnoreCase))
            ? DefaultFields : null;
    }

    public static CandidatePublicationFieldPolicy DefaultPolicy()
        => new(FieldsCode, null, DefaultFields.Split('|'), null, "Decisión aprobada REQ-025");

    public static async Task<CandidatePublicationFieldPolicy> ResolvePolicyAsync(
        PmgmDbContext db, DateOnly asOf, CancellationToken cancellationToken)
    {
        // Status is a lifecycle label; effective intervals govern scheduled/retired versions.
        var row = await db.InstitutionalRuleSettings.AsNoTracking()
            .Where(x => x.Code == FieldsCode &&
                        (x.Status == "active" || x.Status == "scheduled" || x.Status == "retired") &&
                        x.EffectiveFrom <= asOf && (x.EffectiveTo == null || x.EffectiveTo >= asOf))
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return DefaultPolicy();
        if (NormalizeFields(row.Value) is null)
            throw new InvalidOperationException("La política de publicación no corresponde a los campos aprobados.");
        return new(FieldsCode, row.Id, DefaultFields.Split('|'), row.EffectiveFrom,
            row.SourceReference ?? "Sin referencia");
    }

    public static CandidatePublicationFrozenEvidence? ReadMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return null;
        try
        {
            using var json = JsonDocument.Parse(metadata);
            if (json.RootElement.ValueKind != JsonValueKind.Object) return new(0, null, DefaultPolicy());
            if (!json.RootElement.TryGetProperty("publicationEvidence", out var element)) return null;
            var evidence = element.Deserialize<CandidatePublicationFrozenEvidence>(JsonOptions);
            if (evidence is { SchemaVersion: 1, Policy: not null } &&
                evidence.Policy.Code == FieldsCode && evidence.Policy.VisibleFields is not null &&
                NormalizeFields(string.Join('|', evidence.Policy.VisibleFields)) is not null)
                return evidence;
            // Recognized but invalid evidence must not fall back to the mutable profile.
            return new(0, null, DefaultPolicy());
        }
        catch (JsonException)
        {
            // Invalid metadata is not proof that a legacy publication was approved with today's photo.
            return new(0, null, DefaultPolicy());
        }
    }

    public static async Task<IReadOnlyDictionary<Guid, CandidatePublicationFrozenEvidence>> ReadAsync(
        PmgmDbContext db, IEnumerable<Guid> publicationIds, CancellationToken cancellationToken)
    {
        var ids = publicationIds.Select(x => x.ToString()).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, CandidatePublicationFrozenEvidence>();
        var rows = await db.AuditEvents.AsNoTracking()
            .Where(x => x.Action == ApprovalAction && x.EntityType == nameof(CandidatePublication) &&
                        x.Result == AuditResults.Success && ids.Contains(x.EntityId))
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id)
            .Select(x => new { x.EntityId, x.MetadataJson }).ToListAsync(cancellationToken);
        var result = new Dictionary<Guid, CandidatePublicationFrozenEvidence>();
        foreach (var row in rows)
        {
            if (!Guid.TryParse(row.EntityId, out var id) || result.ContainsKey(id)) continue;
            var evidence = ReadMetadata(row.MetadataJson);
            if (evidence is not null) result.Add(id, evidence);
        }
        return result;
    }
}
