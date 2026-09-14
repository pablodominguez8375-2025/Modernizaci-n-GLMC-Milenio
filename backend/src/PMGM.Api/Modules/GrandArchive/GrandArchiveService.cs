using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.GrandArchive.Entities;

namespace PMGM.Api.Modules.GrandArchive;

public interface IGrandArchiveService
{
    Task<GrandArchiveListResponse> ListAsync(GrandArchiveQuery query, CancellationToken cancellationToken);
    Task<GrandArchiveRecordDto?> GetAsync(Guid recordId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GrandArchiveCandidateDto>> CandidatesAsync(string? search, int limit, CancellationToken cancellationToken);
    Task<GrandArchiveRecordDto> RegisterAsync(RegisterGrandArchiveCommand command, ArchiveActor actor, CancellationToken cancellationToken);
    Task<GrandArchiveRecordDto> WithdrawAsync(Guid recordId, string reason, ArchiveActor actor, CancellationToken cancellationToken);
    Task<GrandArchiveContent?> OpenContentAsync(Guid recordId, CancellationToken cancellationToken);
}

public sealed class GrandArchiveService(
    GrandArchiveDbContext archiveDb,
    DocumentManagementDbContext documentDb,
    IDocumentObjectStore objectStore) : IGrandArchiveService
{
    public async Task<GrandArchiveListResponse> ListAsync(GrandArchiveQuery query, CancellationToken cancellationToken)
    {
        IQueryable<GrandArchiveRecord> source = archiveDb.GrandArchiveRecords.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status)) source = source.Where(x => x.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.RecordType)) source = source.Where(x => x.RecordType == query.RecordType);
        if (query.FromDate is not null) source = source.Where(x => x.DocumentDate == null || x.DocumentDate >= query.FromDate);
        if (query.ToDate is not null) source = source.Where(x => x.DocumentDate == null || x.DocumentDate <= query.ToDate);

        var rows = await source
            .OrderByDescending(x => x.DocumentDate)
            .ThenByDescending(x => x.ArchivedAtUtc)
            .Take(1000)
            .ToListAsync(cancellationToken);

        var enriched = await EnrichAsync(rows, cancellationToken);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = Normalize(query.Search);
            enriched = enriched.Where(x => Normalize($"{x.ArchiveCode} {x.Title} {x.DocumentType} {x.RecordType} {x.OriginatingBody} {x.HistoricalPeriod} {x.Description}").Contains(term, StringComparison.Ordinal)).ToList();
        }

        var total = enriched.Count;
        var returned = enriched.Take(query.Limit).ToList();
        return new GrandArchiveListResponse(total, returned.Count, returned);
    }

    public async Task<GrandArchiveRecordDto?> GetAsync(Guid recordId, CancellationToken cancellationToken)
    {
        var record = await archiveDb.GrandArchiveRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recordId, cancellationToken);
        if (record is null) return null;
        return (await EnrichAsync([record], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<GrandArchiveCandidateDto>> CandidatesAsync(string? search, int limit, CancellationToken cancellationToken)
    {
        var archivedVersionIds = await archiveDb.GrandArchiveRecords
            .AsNoTracking()
            .Select(x => x.DocumentVersionId)
            .ToListAsync(cancellationToken);

        var rows = await documentDb.DocumentVersions
            .AsNoTracking()
            .Where(x => x.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available &&
                        !archivedVersionIds.Contains(x.Id) &&
                        x.Document.OrganizationId == null &&
                        x.Document.Collection.Scope == DocumentManagementCodes.Scope.Order &&
                        x.Document.Status != DocumentManagementCodes.DocumentStatus.Draft)
            .Select(x => new CandidateRow(
                x.DocumentId,
                x.Id,
                x.Document.Title,
                x.Document.DocumentType,
                x.Document.Classification,
                x.Document.Status,
                x.VersionNumber,
                x.OriginalFileName,
                x.SizeBytes,
                x.CreatedAtUtc))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(1000)
            .ToListAsync(cancellationToken);

        IEnumerable<CandidateRow> filtered = rows.Where(x => !GrandArchivePolicy.IsForbiddenWorkPaper(x.DocumentType));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = Normalize(search);
            filtered = filtered.Where(x => Normalize($"{x.Title} {x.DocumentType} {x.OriginalFileName}").Contains(term, StringComparison.Ordinal));
        }

        return filtered
            .Take(limit)
            .Select(x => new GrandArchiveCandidateDto(
                x.DocumentId,
                x.VersionId,
                x.Title,
                x.DocumentType,
                x.Classification,
                x.DocumentStatus,
                x.VersionNumber,
                x.OriginalFileName,
                x.SizeBytes,
                x.CreatedAtUtc))
            .ToList();
    }

    public async Task<GrandArchiveRecordDto> RegisterAsync(
        RegisterGrandArchiveCommand command,
        ArchiveActor actor,
        CancellationToken cancellationToken)
    {
        var source = await documentDb.DocumentVersions
            .AsNoTracking()
            .Where(x => x.Id == command.DocumentVersionId)
            .Select(x => new SourceRow(
                x.Id,
                x.DocumentId,
                x.Document.Title,
                x.Document.DocumentType,
                x.Document.OrganizationId,
                x.Document.Collection.Scope,
                x.Document.Status,
                x.ProcessingStatus))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("La versión documental indicada no existe.");

        if (source.DocumentId != command.DocumentId)
            throw new GrandArchivePolicyException("La versión seleccionada no pertenece al documento indicado.");
        if (source.OrganizationId is not null || source.CollectionScope != DocumentManagementCodes.Scope.Order)
            throw new GrandArchivePolicyException("Gran Archivero sólo admite documentos institucionales de ámbito Gran Logia/Orden.");
        if (source.DocumentStatus == DocumentManagementCodes.DocumentStatus.Draft)
            throw new GrandArchivePolicyException("Un documento en borrador no puede incorporarse al archivo histórico.");
        if (source.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available)
            throw new GrandArchivePolicyException("La versión debe haber completado carga, integridad y escaneo antimalware antes de archivarse.");
        if (GrandArchivePolicy.IsForbiddenWorkPaper(source.DocumentType))
            throw new GrandArchivePolicyException("Las planchas de trabajo pertenecen a Biblioteca Virtual y no forman parte de Gran Archivero.");

        var archiveCode = command.ArchiveCode.Trim().ToUpperInvariant();
        var record = new GrandArchiveRecord
        {
            ArchiveCode = archiveCode,
            DocumentId = source.DocumentId,
            DocumentVersionId = source.VersionId,
            RecordType = command.RecordType,
            DocumentDate = command.DocumentDate,
            OriginatingBody = NormalizeOptional(command.OriginatingBody),
            HistoricalPeriod = NormalizeOptional(command.HistoricalPeriod),
            Description = NormalizeOptional(command.Description),
            Status = GrandArchiveCodes.Status.Active,
            CreatedBySubject = actor.Subject,
            CreatedByDisplayName = actor.DisplayName,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        archiveDb.GrandArchiveRecords.Add(record);

        try
        {
            await archiveDb.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            archiveDb.ChangeTracker.Clear();
            var duplicateCode = await archiveDb.GrandArchiveRecords.AsNoTracking().AnyAsync(x => x.ArchiveCode == archiveCode, cancellationToken);
            if (duplicateCode) throw new GrandArchiveDuplicateException("Ya existe un registro con ese código archivístico.");
            throw new GrandArchiveDuplicateException("La versión documental seleccionada ya está incorporada a Gran Archivero.");
        }

        return (await EnrichAsync([record], cancellationToken)).Single();
    }

    public async Task<GrandArchiveRecordDto> WithdrawAsync(
        Guid recordId,
        string reason,
        ArchiveActor actor,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var trimmedReason = reason.Trim();
        var updated = await archiveDb.GrandArchiveRecords
            .Where(x => x.Id == recordId && x.Status == GrandArchiveCodes.Status.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, GrandArchiveCodes.Status.Withdrawn)
                .SetProperty(x => x.WithdrawnAtUtc, now)
                .SetProperty(x => x.WithdrawnBySubject, actor.Subject)
                .SetProperty(x => x.WithdrawnByDisplayName, actor.DisplayName)
                .SetProperty(x => x.WithdrawalReason, trimmedReason)
                .SetProperty(x => x.UpdatedAtUtc, now), cancellationToken);

        if (updated == 0)
        {
            var exists = await archiveDb.GrandArchiveRecords.AsNoTracking().AnyAsync(x => x.Id == recordId, cancellationToken);
            if (!exists) throw new KeyNotFoundException("El registro archivístico no existe.");
            throw new GrandArchivePolicyException("El registro ya fue retirado del catálogo activo.");
        }

        return await GetAsync(recordId, cancellationToken) ?? throw new KeyNotFoundException("El registro archivístico no existe.");
    }

    public async Task<GrandArchiveContent?> OpenContentAsync(Guid recordId, CancellationToken cancellationToken)
    {
        var record = await archiveDb.GrandArchiveRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recordId, cancellationToken);
        if (record is null) return null;
        if (record.Status != GrandArchiveCodes.Status.Active)
            throw new GrandArchivePolicyException("El contenido de un registro retirado no se entrega desde el catálogo activo.");

        var version = await documentDb.DocumentVersions
            .AsNoTracking()
            .Where(x => x.Id == record.DocumentVersionId && x.DocumentId == record.DocumentId)
            .Select(x => new ContentRow(x.ObjectKey, x.OriginalFileName, x.ContentType, x.ProcessingStatus))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new GrandArchiveSourceUnavailableException("La versión documental vinculada ya no está disponible en el Gestor Documental.");

        if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available)
            throw new GrandArchiveSourceUnavailableException("La versión documental vinculada no se encuentra en estado disponible.");
        if (!await objectStore.ExistsAsync(version.ObjectKey, cancellationToken))
            throw new GrandArchiveSourceUnavailableException("El objeto documental vinculado no se encuentra disponible en el almacenamiento institucional.");

        var stream = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken);
        return new GrandArchiveContent(stream, version.ContentType, version.OriginalFileName);
    }

    private async Task<List<GrandArchiveRecordDto>> EnrichAsync(IReadOnlyList<GrandArchiveRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0) return [];
        var versionIds = records.Select(x => x.DocumentVersionId).Distinct().ToList();
        var sources = await documentDb.DocumentVersions
            .AsNoTracking()
            .Where(x => versionIds.Contains(x.Id))
            .Select(x => new EnrichmentRow(
                x.Id,
                x.DocumentId,
                x.Document.Title,
                x.Document.DocumentType,
                x.Document.Classification,
                x.Document.Status,
                x.VersionNumber,
                x.OriginalFileName,
                x.ContentType,
                x.SizeBytes,
                x.ProcessingStatus))
            .ToDictionaryAsync(x => x.VersionId, cancellationToken);

        return records.Select(record =>
        {
            sources.TryGetValue(record.DocumentVersionId, out var source);
            return new GrandArchiveRecordDto(
                record.Id,
                record.ArchiveCode,
                record.DocumentId,
                record.DocumentVersionId,
                record.RecordType,
                record.DocumentDate,
                record.OriginatingBody,
                record.HistoricalPeriod,
                record.Description,
                record.Status,
                record.ArchivedAtUtc,
                record.CreatedByDisplayName,
                record.WithdrawnAtUtc,
                record.WithdrawnByDisplayName,
                record.WithdrawalReason,
                source?.Title ?? "Documento institucional",
                source?.DocumentType ?? "unknown",
                source?.Classification ?? DocumentManagementCodes.Classification.Restricted,
                source?.DocumentStatus ?? "unavailable",
                source?.VersionNumber,
                source?.OriginalFileName,
                source?.ContentType,
                source?.SizeBytes,
                source?.ProcessingStatus ?? "unavailable");
        }).ToList();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Normalize(string value)
        => string.Concat(value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant()
            .Trim();

    private sealed record CandidateRow(Guid DocumentId, Guid VersionId, string Title, string DocumentType, string Classification, string DocumentStatus, int VersionNumber, string OriginalFileName, long SizeBytes, DateTimeOffset CreatedAtUtc);
    private sealed record SourceRow(Guid VersionId, Guid DocumentId, string Title, string DocumentType, Guid? OrganizationId, string CollectionScope, string DocumentStatus, string ProcessingStatus);
    private sealed record ContentRow(string ObjectKey, string OriginalFileName, string ContentType, string ProcessingStatus);
    private sealed record EnrichmentRow(Guid VersionId, Guid DocumentId, string Title, string DocumentType, string Classification, string DocumentStatus, int VersionNumber, string OriginalFileName, string ContentType, long SizeBytes, string ProcessingStatus);
}

public sealed record ArchiveActor(string Subject, string? DisplayName);
public sealed record GrandArchiveQuery(string? Status, string? RecordType, DateOnly? FromDate, DateOnly? ToDate, string? Search, int Limit);
public sealed record GrandArchiveListResponse(int Total, int Returned, IReadOnlyList<GrandArchiveRecordDto> Items);
public sealed record RegisterGrandArchiveCommand(Guid DocumentId, Guid DocumentVersionId, string ArchiveCode, string RecordType, DateOnly? DocumentDate, string? OriginatingBody, string? HistoricalPeriod, string? Description);
public sealed record GrandArchiveCandidateDto(Guid DocumentId, Guid DocumentVersionId, string Title, string DocumentType, string Classification, string DocumentStatus, int VersionNumber, string OriginalFileName, long SizeBytes, DateTimeOffset CreatedAtUtc);
public sealed record GrandArchiveContent(Stream Content, string ContentType, string FileName);

public sealed record GrandArchiveRecordDto(
    Guid Id,
    string ArchiveCode,
    Guid DocumentId,
    Guid DocumentVersionId,
    string RecordType,
    DateOnly? DocumentDate,
    string? OriginatingBody,
    string? HistoricalPeriod,
    string? Description,
    string Status,
    DateTimeOffset ArchivedAtUtc,
    string? CreatedByDisplayName,
    DateTimeOffset? WithdrawnAtUtc,
    string? WithdrawnByDisplayName,
    string? WithdrawalReason,
    string Title,
    string DocumentType,
    string Classification,
    string DocumentStatus,
    int? VersionNumber,
    string? OriginalFileName,
    string? ContentType,
    long? SizeBytes,
    string ProcessingStatus);

public sealed class GrandArchivePolicyException(string message) : Exception(message);
public sealed class GrandArchiveDuplicateException(string message) : Exception(message);
public sealed class GrandArchiveSourceUnavailableException(string message) : Exception(message);
