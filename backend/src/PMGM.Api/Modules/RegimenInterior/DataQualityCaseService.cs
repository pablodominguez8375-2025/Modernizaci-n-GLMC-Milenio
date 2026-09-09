using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

public interface IDataQualityCaseService
{
    Task<OpenDataQualityCaseResult> OpenAsync(OpenDataQualityCaseCommand command, CaseActor actor, CancellationToken cancellationToken);
    Task<DataQualityCaseListResponse> ListAsync(DataQualityCaseListQuery query, string? actorSubject, CancellationToken cancellationToken);
    Task<DataQualityCaseDto?> GetAsync(Guid caseId, CancellationToken cancellationToken);
    Task<DataQualityCaseDto> ClaimAsync(Guid caseId, CaseActor actor, CancellationToken cancellationToken);
    Task<DataQualityCaseDto> ResolveAsync(Guid caseId, ResolveDataQualityCaseCommand command, CaseActor actor, bool allowOverride, CancellationToken cancellationToken);
}

public sealed class DataQualityCaseService(
    RegimenInteriorDbContext caseDb,
    PmgmDbContext coreDb,
    IRegimenInteriorDataQualityService dataQualityService) : IDataQualityCaseService
{
    public async Task<OpenDataQualityCaseResult> OpenAsync(
        OpenDataQualityCaseCommand command,
        CaseActor actor,
        CancellationToken cancellationToken)
    {
        var member = await coreDb.Members
            .AsNoTracking()
            .Where(x => x.Id == command.MemberId)
            .Select(x => new MemberIdentity(x.Id, x.InstitutionalNumber, x.Person.FirstNames + " " + x.Person.LastNames))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("El miembro indicado no existe.");

        if (command.OrganizationId is not null)
        {
            var organizationExists = await coreDb.Organizations
                .AsNoTracking()
                .AnyAsync(x => x.Id == command.OrganizationId.Value, cancellationToken);
            if (!organizationExists) throw new KeyNotFoundException("El Taller u organización indicada no existe.");
        }

        var search = member.InstitutionalNumber ?? member.DisplayName;
        var currentFindings = await dataQualityService.QueryAsync(new DataQualityQuery(
            command.DetectionAsOf,
            command.OrganizationId,
            command.Severity,
            command.RuleCode,
            search,
            1000), cancellationToken);

        var finding = currentFindings.Items.SingleOrDefault(x =>
            x.MemberId == command.MemberId &&
            x.OrganizationId == command.OrganizationId &&
            string.Equals(x.Code, command.RuleCode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Severity, command.Severity, StringComparison.OrdinalIgnoreCase) &&
            x.PrimaryDate == command.PrimaryDate &&
            x.RelatedDate == command.RelatedDate);

        if (finding is null)
        {
            throw new DataQualityFindingNotCurrentException();
        }

        var fingerprint = Fingerprint(finding.Code, finding.MemberId, finding.OrganizationId);
        var existing = await ActiveByFingerprintAsync(fingerprint, cancellationToken);
        if (existing is not null)
        {
            return new OpenDataQualityCaseResult(await ToDtoAsync(existing, false, cancellationToken), false);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new DataQualityCase
        {
            IssueFingerprint = fingerprint,
            RuleCode = finding.Code,
            Severity = finding.Severity,
            MemberId = finding.MemberId,
            OrganizationId = finding.OrganizationId,
            DetectionAsOf = command.DetectionAsOf,
            PrimaryDate = finding.PrimaryDate,
            RelatedDate = finding.RelatedDate,
            Status = DataQualityCaseCodes.Status.Open,
            CreatedBySubject = actor.Subject,
            CreatedByDisplayName = actor.DisplayName,
            UpdatedAtUtc = now
        };
        entity.Events.Add(new DataQualityCaseEvent
        {
            DataQualityCaseId = entity.Id,
            Action = DataQualityCaseCodes.Action.Opened,
            FromStatus = null,
            ToStatus = DataQualityCaseCodes.Status.Open,
            ActorSubject = actor.Subject,
            ActorDisplayName = actor.DisplayName
        });
        caseDb.DataQualityCases.Add(entity);

        try
        {
            await caseDb.SaveChangesAsync(cancellationToken);
            return new OpenDataQualityCaseResult(await ToDtoAsync(entity, true, cancellationToken), true);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            caseDb.ChangeTracker.Clear();
            existing = await ActiveByFingerprintAsync(fingerprint, cancellationToken);
            if (existing is null) throw;
            return new OpenDataQualityCaseResult(await ToDtoAsync(existing, false, cancellationToken), false);
        }
    }

    public async Task<DataQualityCaseListResponse> ListAsync(
        DataQualityCaseListQuery query,
        string? actorSubject,
        CancellationToken cancellationToken)
    {
        IQueryable<DataQualityCase> source = caseDb.DataQualityCases.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status)) source = source.Where(x => x.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.RuleCode)) source = source.Where(x => x.RuleCode == query.RuleCode);
        if (query.OrganizationId is not null) source = source.Where(x => x.OrganizationId == query.OrganizationId.Value);
        if (query.AssignedToMe)
        {
            if (string.IsNullOrWhiteSpace(actorSubject)) return new DataQualityCaseListResponse(0, 0, []);
            source = source.Where(x => x.AssignedToSubject == actorSubject);
        }

        var total = await source.CountAsync(cancellationToken);
        var entities = await source
            .OrderBy(x => x.Status == DataQualityCaseCodes.Status.Open ? 0 : x.Status == DataQualityCaseCodes.Status.UnderReview ? 1 : 2)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
        var items = await EnrichAsync(entities, includeEvents: false, cancellationToken);
        return new DataQualityCaseListResponse(total, items.Count, items);
    }

    public async Task<DataQualityCaseDto?> GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var entity = await caseDb.DataQualityCases
            .AsNoTracking()
            .Include(x => x.Events)
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        return entity is null ? null : await ToDtoAsync(entity, true, cancellationToken);
    }

    public async Task<DataQualityCaseDto> ClaimAsync(Guid caseId, CaseActor actor, CancellationToken cancellationToken)
    {
        var entity = await caseDb.DataQualityCases
            .Include(x => x.Events)
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken)
            ?? throw new KeyNotFoundException("El caso de corroboración no existe.");

        if (!DataQualityCaseCodes.Status.IsActive(entity.Status))
            throw new InvalidCaseTransitionException("Sólo un caso abierto o en revisión puede ser tomado.");

        if (entity.Status == DataQualityCaseCodes.Status.UnderReview)
        {
            if (string.Equals(entity.AssignedToSubject, actor.Subject, StringComparison.Ordinal))
                return await ToDtoAsync(entity, true, cancellationToken);
            throw new CaseAlreadyAssignedException();
        }

        var from = entity.Status;
        entity.Status = DataQualityCaseCodes.Status.UnderReview;
        entity.AssignedToSubject = actor.Subject;
        entity.AssignedToDisplayName = actor.DisplayName;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        entity.Events.Add(new DataQualityCaseEvent
        {
            DataQualityCaseId = entity.Id,
            Action = DataQualityCaseCodes.Action.Claimed,
            FromStatus = from,
            ToStatus = entity.Status,
            ActorSubject = actor.Subject,
            ActorDisplayName = actor.DisplayName
        });
        await caseDb.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(entity, true, cancellationToken);
    }

    public async Task<DataQualityCaseDto> ResolveAsync(
        Guid caseId,
        ResolveDataQualityCaseCommand command,
        CaseActor actor,
        bool allowOverride,
        CancellationToken cancellationToken)
    {
        var entity = await caseDb.DataQualityCases
            .Include(x => x.Events)
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken)
            ?? throw new KeyNotFoundException("El caso de corroboración no existe.");

        if (entity.Status != DataQualityCaseCodes.Status.UnderReview)
            throw new InvalidCaseTransitionException("El caso debe estar en revisión antes de resolverlo.");
        if (!allowOverride && !string.Equals(entity.AssignedToSubject, actor.Subject, StringComparison.Ordinal))
            throw new CaseAssignedToAnotherReviewerException();

        var targetStatus = command.Outcome switch
        {
            DataQualityResolutionOutcome.Confirmed => DataQualityCaseCodes.Status.ResolvedConfirmed,
            DataQualityResolutionOutcome.Dismissed => DataQualityCaseCodes.Status.Dismissed,
            _ => throw new ArgumentException("El resultado de resolución no es válido.", nameof(command))
        };

        var from = entity.Status;
        var now = DateTimeOffset.UtcNow;
        entity.Status = targetStatus;
        entity.ResolutionSummary = command.ResolutionSummary.Trim();
        entity.EvidenceReference = string.IsNullOrWhiteSpace(command.EvidenceReference) ? null : command.EvidenceReference.Trim();
        entity.ResolvedBySubject = actor.Subject;
        entity.ResolvedAtUtc = now;
        entity.UpdatedAtUtc = now;
        entity.Events.Add(new DataQualityCaseEvent
        {
            DataQualityCaseId = entity.Id,
            Action = targetStatus == DataQualityCaseCodes.Status.ResolvedConfirmed
                ? DataQualityCaseCodes.Action.ResolvedConfirmed
                : DataQualityCaseCodes.Action.Dismissed,
            FromStatus = from,
            ToStatus = targetStatus,
            ActorSubject = actor.Subject,
            ActorDisplayName = actor.DisplayName
        });
        await caseDb.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(entity, true, cancellationToken);
    }

    private Task<DataQualityCase?> ActiveByFingerprintAsync(string fingerprint, CancellationToken cancellationToken)
        => caseDb.DataQualityCases
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.IssueFingerprint == fingerprint &&
                                       (x.Status == DataQualityCaseCodes.Status.Open || x.Status == DataQualityCaseCodes.Status.UnderReview),
                cancellationToken);

    private async Task<DataQualityCaseDto> ToDtoAsync(DataQualityCase entity, bool includeEvents, CancellationToken cancellationToken)
        => (await EnrichAsync([entity], includeEvents, cancellationToken)).Single();

    private async Task<IReadOnlyList<DataQualityCaseDto>> EnrichAsync(
        IReadOnlyList<DataQualityCase> entities,
        bool includeEvents,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return [];
        var memberIds = entities.Select(x => x.MemberId).Distinct().ToList();
        var organizationIds = entities.Where(x => x.OrganizationId != null).Select(x => x.OrganizationId!.Value).Distinct().ToList();

        var members = await coreDb.Members
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.Id))
            .Select(x => new MemberIdentity(x.Id, x.InstitutionalNumber, x.Person.FirstNames + " " + x.Person.LastNames))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var organizations = organizationIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await coreDb.Organizations.AsNoTracking()
                .Where(x => organizationIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return entities.Select(entity =>
        {
            members.TryGetValue(entity.MemberId, out var member);
            var organizationName = entity.OrganizationId is not null && organizations.TryGetValue(entity.OrganizationId.Value, out var name) ? name : null;
            var events = includeEvents
                ? entity.Events.OrderBy(x => x.OccurredAtUtc).Select(x => new DataQualityCaseEventDto(
                    x.Id, x.Action, x.FromStatus, x.ToStatus, x.ActorSubject, x.ActorDisplayName, x.OccurredAtUtc)).ToList()
                : [];
            return new DataQualityCaseDto(
                entity.Id,
                entity.RuleCode,
                entity.Severity,
                entity.MemberId,
                member?.InstitutionalNumber,
                member?.DisplayName ?? "Miembro institucional",
                entity.OrganizationId,
                organizationName,
                entity.DetectionAsOf,
                entity.PrimaryDate,
                entity.RelatedDate,
                entity.Status,
                entity.AssignedToSubject,
                entity.AssignedToDisplayName,
                entity.CreatedBySubject,
                entity.CreatedByDisplayName,
                entity.CreatedAtUtc,
                entity.UpdatedAtUtc,
                entity.ResolutionSummary,
                entity.EvidenceReference,
                entity.ResolvedBySubject,
                entity.ResolvedAtUtc,
                events);
        }).ToList();
    }

    private static string Fingerprint(string ruleCode, Guid memberId, Guid? organizationId)
    {
        var normalized = $"{ruleCode.Trim().ToLowerInvariant()}|{memberId:D}|{organizationId?.ToString("D") ?? "order"}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private sealed record MemberIdentity(Guid Id, string? InstitutionalNumber, string DisplayName);
}

public sealed record CaseActor(string Subject, string? DisplayName);

public sealed record OpenDataQualityCaseCommand(
    DateOnly DetectionAsOf,
    string RuleCode,
    string Severity,
    Guid MemberId,
    Guid? OrganizationId,
    DateOnly? PrimaryDate,
    DateOnly? RelatedDate);

public sealed record OpenDataQualityCaseResult(DataQualityCaseDto Case, bool Created);

public sealed record DataQualityCaseListQuery(
    string? Status,
    string? RuleCode,
    Guid? OrganizationId,
    bool AssignedToMe,
    int Limit);

public sealed record DataQualityCaseListResponse(
    int Total,
    int Returned,
    IReadOnlyList<DataQualityCaseDto> Items);

public static class DataQualityResolutionOutcome
{
    public const string Confirmed = "confirmed";
    public const string Dismissed = "dismissed";
}

public sealed record ResolveDataQualityCaseCommand(
    string Outcome,
    string ResolutionSummary,
    string? EvidenceReference);

public sealed record DataQualityCaseDto(
    Guid Id,
    string RuleCode,
    string Severity,
    Guid MemberId,
    string? InstitutionalNumber,
    string DisplayName,
    Guid? OrganizationId,
    string? OrganizationName,
    DateOnly DetectionAsOf,
    DateOnly? PrimaryDate,
    DateOnly? RelatedDate,
    string Status,
    string? AssignedToSubject,
    string? AssignedToDisplayName,
    string CreatedBySubject,
    string? CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? ResolutionSummary,
    string? EvidenceReference,
    string? ResolvedBySubject,
    DateTimeOffset? ResolvedAtUtc,
    IReadOnlyList<DataQualityCaseEventDto> Events);

public sealed record DataQualityCaseEventDto(
    Guid Id,
    string Action,
    string? FromStatus,
    string ToStatus,
    string ActorSubject,
    string? ActorDisplayName,
    DateTimeOffset OccurredAtUtc);

public sealed class DataQualityFindingNotCurrentException : Exception
{
    public DataQualityFindingNotCurrentException() : base("El hallazgo ya no está presente con los datos y la fecha de corte indicados.") { }
}

public sealed class InvalidCaseTransitionException(string message) : Exception(message);
public sealed class CaseAlreadyAssignedException : Exception
{
    public CaseAlreadyAssignedException() : base("El caso ya está siendo revisado por otra persona.") { }
}
public sealed class CaseAssignedToAnotherReviewerException : Exception
{
    public CaseAssignedToAnotherReviewerException() : base("El caso está asignado a otra persona y no puede ser resuelto por este usuario.") { }
}
