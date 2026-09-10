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

public sealed class LodgeCorrespondenceRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string Folio { get; set; }
    public required string Direction { get; set; }
    public DateOnly CorrespondenceDate { get; set; }
    public required string Subject { get; set; }
    public required string Counterparty { get; set; }
    public required string Channel { get; set; }
    public string? ExternalReference { get; set; }
    public required string Status { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? UpdatedBySubject { get; set; }
}

public sealed class LodgeSecretariatTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string Title { get; set; }
    public string? Detail { get; set; }
    public DateOnly? DueDate { get; set; }
    public required string Priority { get; set; }
    public string? ResponsibleLabel { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CompletedBySubject { get; set; }
}

public sealed class LodgeMeetingAgendaItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public LodgeMeeting Meeting { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public int Position { get; set; }
    public required string Title { get; set; }
    public string? Detail { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? UpdatedBySubject { get; set; }
}
