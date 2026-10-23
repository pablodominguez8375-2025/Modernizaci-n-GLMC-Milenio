namespace PMGM.Api.Modules.GrandArchive.Entities;

public sealed class GrandArchiveRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ArchiveCode { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public string RecordType { get; set; } = GrandArchiveCodes.RecordType.HistoricalRecord;
    public DateOnly? DocumentDate { get; set; }
    public string? OriginatingBody { get; set; }
    public string? HistoricalPeriod { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = GrandArchiveCodes.Status.Active;
    public DateTimeOffset ArchivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBySubject { get; set; } = string.Empty;
    public string? CreatedByDisplayName { get; set; }
    public DateTimeOffset? WithdrawnAtUtc { get; set; }
    public string? WithdrawnBySubject { get; set; }
    public string? WithdrawnByDisplayName { get; set; }
    public string? WithdrawalReason { get; set; }
    // Publicación a los Hermanos (#653): grado mínimo 1, 2 o 3; null = no publicado.
    public int? PublishedMinimumDegree { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    /// <summary>Fin de la vigencia de la publicación; al vencer el documento queda archivado (solo lo ve el Gran Archivero). Null = sin vencimiento.</summary>
    public DateTimeOffset? PublishedUntilUtc { get; set; }
    public string? PublishedBySubject { get; set; }
    public string? PublishedByDisplayName { get; set; }
    public DateTimeOffset? UnpublishedAtUtc { get; set; }
    public string? UnpublishReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
