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
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
