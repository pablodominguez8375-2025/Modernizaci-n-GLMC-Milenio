namespace PMGM.Api.Modules.LodgeManagement.Entities;

public sealed class LodgeCouncilSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public DateOnly SessionDate { get; set; }
    public string? Title { get; set; }
    public required string Status { get; set; }
    public bool QualifiedQuorumConfirmed { get; set; }
    public string? QuorumConfirmedBySubject { get; set; }
    public DateTimeOffset? QuorumConfirmedAtUtc { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAtUtc { get; set; }
}

public sealed class LodgeCouncilAttendanceRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public LodgeCouncilSession Session { get; set; } = null!;
    public Guid? MemberId { get; set; }
    public required string DisplayName { get; set; }
    public string? InstitutionalRole { get; set; }
    public required string ParticipationType { get; set; }
    public required string Status { get; set; }
    public bool HasVoice { get; set; }
    public bool HasVote { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeCouncilDecision
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public LodgeCouncilSession Session { get; set; } = null!;
    public required string Category { get; set; }
    public required string Subject { get; set; }
    public required string Resolution { get; set; }
    public required string Outcome { get; set; }
    public bool RequiresChamberReview { get; set; }
    public string? ChamberReference { get; set; }
    public Guid? SupportingDocumentId { get; set; }
    public decimal? Amount { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeCouncilFinancialReview
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public LodgeCouncilSession Session { get; set; } = null!;
    public required string ControlArea { get; set; }
    public required string PeriodLabel { get; set; }
    public required string Conclusion { get; set; }
    public string? Observations { get; set; }
    public Guid? SupportingDocumentId { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
