namespace PMGM.Api.Modules.SecretariatOperations.Entities;

public sealed class HistoricalMemberIntake
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? TargetMemberId { get; set; }
    public DateOnly CutoffDate { get; set; }
    public required string FirstNames { get; set; }
    public required string LastNames { get; set; }
    public string? Rut { get; set; }
    public string? InstitutionalNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public required string CurrentDegree { get; set; }
    public DateOnly? MembershipStartDate { get; set; }
    public DateOnly? InitiationDate { get; set; }
    public DateOnly? WageIncreaseDate { get; set; }
    public DateOnly? ExaltationDate { get; set; }
    public required string EvidenceReference { get; set; }
    public required string Status { get; set; }
    public int Revision { get; set; } = 1;
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public string? ReviewedBySubject { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? ApprovedMemberId { get; set; }
    public List<HistoricalMemberIntakeOffice> Offices { get; set; } = [];
}

public sealed class HistoricalMemberIntakeOffice
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid IntakeId { get; set; }
    public HistoricalMemberIntake Intake { get; set; } = null!;
    public required string OfficeType { get; set; }
    public required string Period { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public sealed class LodgeAdministrativeMeeting
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public DateOnly MeetingDate { get; set; }
    public required string Title { get; set; }
    public string? Purpose { get; set; }
    public required string Status { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? HeldAtUtc { get; set; }
}

public sealed class LodgeSecretariatRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string RecordType { get; set; }
    public Guid SourceRecordId { get; set; }
    public DateOnly EventDate { get; set; }
    public required string Title { get; set; }
    public Guid? WorkPaperDocumentVersionId { get; set; }
    public Guid? WorkPaperAuthorMemberId { get; set; }
    public Guid? ExtractDocumentVersionId { get; set; }
    public Guid? FullMinuteDocumentVersionId { get; set; }
    public Guid? CeremonyAuthorizationDocumentId { get; set; }
    public required string Status { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? SubmittedBySubject { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public string? ReviewedBySubject { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
}

public sealed class LodgeCorrespondence
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string Direction { get; set; }
    public required string Folio { get; set; }
    public DateOnly CorrespondenceDate { get; set; }
    public required string Subject { get; set; }
    public required string Counterparty { get; set; }
    public required string Channel { get; set; }
    public string? Reference { get; set; }
    public required string Status { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? ClosedBySubject { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
}

public sealed class LodgeSecretariatTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public required string Title { get; set; }
    public string? Detail { get; set; }
    public DateOnly? DueDate { get; set; }
    public required string Priority { get; set; }
    public string? Responsible { get; set; }
    public required string Status { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? CompletedBySubject { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

public sealed class LodgeAgendaItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? MeetingId { get; set; }
    public int Order { get; set; }
    public required string Title { get; set; }
    public string? Detail { get; set; }
    public required string Status { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? UpdatedBySubject { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
