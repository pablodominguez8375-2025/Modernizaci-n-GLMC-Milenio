namespace PMGM.Api.Modules.RegimenInterior.Entities;

public sealed class DataQualityCase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string IssueFingerprint { get; set; }
    public required string RuleCode { get; set; }
    public required string Severity { get; set; }
    public Guid MemberId { get; set; }
    public Guid? OrganizationId { get; set; }
    public DateOnly DetectionAsOf { get; set; }
    public DateOnly? PrimaryDate { get; set; }
    public DateOnly? RelatedDate { get; set; }
    public required string Status { get; set; }
    public string? AssignedToSubject { get; set; }
    public string? AssignedToDisplayName { get; set; }
    public required string CreatedBySubject { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ResolutionSummary { get; set; }
    public string? EvidenceReference { get; set; }
    public string? ResolvedBySubject { get; set; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
    public List<DataQualityCaseEvent> Events { get; } = [];
}

public sealed class DataQualityCaseEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DataQualityCaseId { get; set; }
    public DataQualityCase DataQualityCase { get; set; } = null!;
    public required string Action { get; set; }
    public string? FromStatus { get; set; }
    public required string ToStatus { get; set; }
    public required string ActorSubject { get; set; }
    public string? ActorDisplayName { get; set; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
