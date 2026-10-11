namespace PMGM.Api.Modules.CandidateIntake.Entities;

/// <summary>Designación privada asociada al expediente: nunca se publica en el portal transversal.</summary>
public sealed class CandidateInterviewAssignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CeremonyRequestId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid InterviewerMemberId { get; set; }
    public int Position { get; set; }
    public required string CouncilBody { get; set; }
    public DateOnly CouncilDecisionDate { get; set; }
    public required string CouncilMinuteReference { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public required string Status { get; set; }
    public required string AssignedBySubject { get; set; }
    public DateTimeOffset AssignedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? ReplacedBySubject { get; set; }
    public DateTimeOffset? ReplacedAtUtc { get; set; }
    public string? ReplacementReason { get; set; }
    public Guid? ReportDocumentVersionId { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public DateTimeOffset? NotificationQueuedAtUtc { get; set; }
}
