using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Privacy.Entities;

public sealed class DataProcessingActivity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Module { get; set; }
    public required string Purpose { get; set; }
    public required string LawfulBasis { get; set; }
    public required string DataCategoriesJson { get; set; }
    public required string SubjectCategoriesJson { get; set; }
    public bool ContainsSensitiveData { get; set; }
    public string? RecipientsJson { get; set; }
    public bool HasInternationalTransfer { get; set; }
    public Guid? RetentionPolicyId { get; set; }
    public DataRetentionPolicy? RetentionPolicy { get; set; }
    public string? InternalOwner { get; set; }
    public required string Status { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DataRetentionPolicy
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string DataCategory { get; set; }
    public required string Purpose { get; set; }
    public required string LegalBasis { get; set; }
    public int? RetentionDays { get; set; }
    public string? ExpirationEvent { get; set; }
    public required string ExpirationAction { get; set; }
    public bool AllowsLegalHold { get; set; }
    public required string Status { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<DataRetentionHold> Holds { get; set; } = new List<DataRetentionHold>();
    public ICollection<DataRetentionEvaluation> Evaluations { get; set; } = new List<DataRetentionEvaluation>();
}

[Table("data_retention_holds", Schema = "core")]
[Index(nameof(EntityType), nameof(EntityId), nameof(Status))]
[Index(nameof(RetentionPolicyId), nameof(EffectiveFrom))]
public sealed class DataRetentionHold
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RetentionPolicyId { get; set; }
    public DataRetentionPolicy RetentionPolicy { get; set; } = null!;
    [MaxLength(120)]
    public required string EntityType { get; set; }
    [MaxLength(160)]
    public required string EntityId { get; set; }
    [MaxLength(2000)]
    public required string Reason { get; set; }
    [MaxLength(320)]
    public required string AuthoritySubject { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    [MaxLength(40)]
    public required string Status { get; set; }
    [MaxLength(500)]
    public string? EvidenceReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

[Table("data_retention_evaluations", Schema = "core")]
[Index(nameof(EntityType), nameof(EntityId), nameof(EvaluatedAtUtc))]
[Index(nameof(RetentionPolicyId), nameof(EvaluatedAtUtc))]
public sealed class DataRetentionEvaluation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RetentionPolicyId { get; set; }
    public DataRetentionPolicy RetentionPolicy { get; set; } = null!;
    [MaxLength(120)]
    public required string EntityType { get; set; }
    [MaxLength(160)]
    public required string EntityId { get; set; }
    public DateOnly AnchorDate { get; set; }
    public DateOnly EvaluationDate { get; set; }
    public DateOnly? DueDate { get; set; }
    [MaxLength(80)]
    public required string RecommendedAction { get; set; }
    public bool BlockedByHold { get; set; }
    public Guid? RetentionHoldId { get; set; }
    [MaxLength(2000)]
    public required string Rationale { get; set; }
    [MaxLength(40)]
    public required string Status { get; set; }
    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DataSubjectRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? PersonId { get; set; }
    public Person? Person { get; set; }
    public required string RequestType { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public required string Channel { get; set; }
    public bool IdentityVerified { get; set; }
    public required string Status { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? ExtensionUntil { get; set; }
    public string? ResponsibleSubject { get; set; }
    public string? Resolution { get; set; }
    public string? Grounds { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DataProcessor
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Service { get; set; }
    public required string Purpose { get; set; }
    public required string CountriesJson { get; set; }
    public required string DataCategoriesJson { get; set; }
    public string? SubjectCategoriesJson { get; set; }
    public string? SubprocessorsJson { get; set; }
    public string? AgreementReference { get; set; }
    public string? TransferMechanism { get; set; }
    public string? IncidentObligations { get; set; }
    public required string Status { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class InternationalDataTransfer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DataProcessingActivityId { get; set; }
    public DataProcessingActivity DataProcessingActivity { get; set; } = null!;
    public Guid? DataProcessorId { get; set; }
    public DataProcessor? DataProcessor { get; set; }
    public required string DestinationCountry { get; set; }
    public required string Recipient { get; set; }
    public required string LegalMechanism { get; set; }
    public string? Safeguards { get; set; }
    public required string Status { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PrivacySecurityIncident
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset DetectedAtUtc { get; set; }
    public required string Source { get; set; }
    public required string Nature { get; set; }
    public required string DataCategoriesJson { get; set; }
    public bool InvolvesSensitiveData { get; set; }
    public int? EstimatedSubjects { get; set; }
    public required string RiskLevel { get; set; }
    public string? ImmediateMeasures { get; set; }
    public required string Status { get; set; }

    public DateTimeOffset? AssessmentCompletedAtUtc { get; set; }
    [MaxLength(4000)]
    public string? AssessmentSummary { get; set; }

    public bool NotifyAuthority { get; set; }
    [MaxLength(40)]
    public string? AuthorityDecision { get; set; }
    public DateTimeOffset? AuthorityDecisionAtUtc { get; set; }
    [MaxLength(4000)]
    public string? AuthorityDecisionReason { get; set; }
    public DateTimeOffset? AuthorityNotifiedAtUtc { get; set; }
    [MaxLength(120)]
    public string? AuthorityNotificationChannel { get; set; }
    public string? AuthorityReference { get; set; }

    public bool NotifySubjects { get; set; }
    [MaxLength(40)]
    public string? SubjectsDecision { get; set; }
    public DateTimeOffset? SubjectsDecisionAtUtc { get; set; }
    [MaxLength(4000)]
    public string? SubjectsDecisionReason { get; set; }
    public DateTimeOffset? SubjectsNotifiedAtUtc { get; set; }
    [MaxLength(120)]
    public string? SubjectsNotificationChannel { get; set; }
    [MaxLength(500)]
    public string? SubjectsNotificationReference { get; set; }

    public string? CorrectiveActions { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    [MaxLength(320)]
    public string? ClosedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PrivacyImpactAssessment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DataProcessingActivityId { get; set; }
    public DataProcessingActivity DataProcessingActivity { get; set; } = null!;
    public required string RiskLevel { get; set; }
    public bool HighRisk { get; set; }
    public required string Status { get; set; }
    public required string AssessmentSummary { get; set; }
    public string? Mitigations { get; set; }
    public string? ResidualRisk { get; set; }
    public string? ApprovedBySubject { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
