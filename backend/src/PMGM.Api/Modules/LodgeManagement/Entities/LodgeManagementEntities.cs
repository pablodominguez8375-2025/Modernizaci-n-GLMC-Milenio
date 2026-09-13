namespace PMGM.Api.Modules.LodgeManagement.Entities;

public sealed class LodgeMeeting
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public DateOnly MeetingDate { get; set; }
    public required string MeetingType { get; set; }
    public required string Grade { get; set; }
    public string? Title { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAtUtc { get; set; }
}

public sealed class LodgeAttendanceRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public LodgeMeeting Meeting { get; set; } = null!;
    public Guid MemberId { get; set; }
    public required string Status { get; set; }
    public string? ExcuseReason { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeMinute
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public LodgeMeeting Meeting { get; set; } = null!;
    public int Version { get; set; }
    public required string Content { get; set; }
    public required string Status { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? ApprovedBySubject { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
}

public sealed class LodgeAnonymousBallot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public LodgeMeeting Meeting { get; set; } = null!;
    public int Version { get; set; }
    public required string BallotType { get; set; }
    public int? ProcedureNumber { get; set; }
    public required string Subject { get; set; }
    public int AttendeeCount { get; set; }
    public int EligibleCount { get; set; }
    public int PositiveCount { get; set; }
    public int NegativeCount { get; set; }
    public string? RecountObservation { get; set; }
    public required string Status { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeInstructionSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public DateOnly InstructionDate { get; set; }
    public required string Grade { get; set; }
    public required string Topic { get; set; }
    public required string ResponsibleOffice { get; set; }
    public Guid? InstructorMemberId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string CreatedBySubject { get; set; }
}

public sealed class LodgeInstructionAttendanceRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid InstructionSessionId { get; set; }
    public LodgeInstructionSession InstructionSession { get; set; } = null!;
    public Guid MemberId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string RecordedBySubject { get; set; }
}
