namespace PMGM.Api.Modules.AssemblyGovernance.Entities;

public sealed class InstitutionalAssembly
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Type { get; set; }
    public DateOnly AssemblyDate { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset? RosterCutoffUtc { get; set; }
    public DateTimeOffset? FrozenAtUtc { get; set; }
    public string? FrozenBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AssemblyMemberStatus
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Guid LodgeId { get; set; }
    public required string Category { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public Guid? SourceOfficeAssignmentId { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AssemblyEligibility
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public Guid PersonId { get; set; }
    public Guid LodgeId { get; set; }
    public bool CanAttend { get; set; }
    public bool CanVote { get; set; }
    public required string Status { get; set; }
    public string? PrimaryReason { get; set; }
    public DateTimeOffset EvaluatedAtUtc { get; set; }
    public required string EvaluatedBySubject { get; set; }
    public Guid? FinancialSnapshotId { get; set; }
    public string? RightsSnapshotReference { get; set; }
    public bool IsFrozenSnapshot { get; set; }
    public DateTimeOffset? FrozenAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class InstitutionalRestriction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public required string Origin { get; set; }
    public required string RestrictionType { get; set; }
    public bool AffectsAttendance { get; set; }
    public bool AffectsVoting { get; set; }
    public bool AffectsMasonicRights { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public required string Grounds { get; set; }
    public string? CaseReference { get; set; }
    public bool Active { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
