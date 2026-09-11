namespace PMGM.Api.Modules.Admissions.Entities;

public sealed class AdmissionCase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string AdmissionType { get; set; }
    public string? AffiliationMode { get; set; }
    public Guid? MemberId { get; set; }
    public Guid PersonId { get; set; }
    public Guid? OriginOrganizationId { get; set; }
    public string? OriginLodgeName { get; set; }
    public string? OriginLodgeNumber { get; set; }
    public string? OriginObedience { get; set; }
    public string? Degree { get; set; }
    public bool? HasPeaceAndFriendshipPact { get; set; }
    public DateOnly? PreviousRejectionDate { get; set; }
    public bool? RejectionCausesRemedied { get; set; }
    public required string Status { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<AdmissionEvidence> Evidence { get; set; } = new List<AdmissionEvidence>();
    public ICollection<AdmissionDecision> Decisions { get; set; } = new List<AdmissionDecision>();
}

public sealed class AdmissionEvidence
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AdmissionCaseId { get; set; }
    public AdmissionCase AdmissionCase { get; set; } = null!;
    public required string EvidenceType { get; set; }
    public Guid? DocumentVersionId { get; set; }
    public DateOnly? EvidenceDate { get; set; }
    public string? SourceReference { get; set; }
    public required string ReviewStatus { get; set; }
    public string? ReviewedBySubject { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? Notes { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AdmissionDecision
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AdmissionCaseId { get; set; }
    public AdmissionCase AdmissionCase { get; set; } = null!;
    public required string DecisionType { get; set; }
    public required string Status { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
