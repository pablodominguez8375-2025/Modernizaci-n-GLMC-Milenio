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
    public bool NotifyAuthority { get; set; }
    public bool NotifySubjects { get; set; }
    public DateTimeOffset? AuthorityNotifiedAtUtc { get; set; }
    public string? AuthorityReference { get; set; }
    public DateTimeOffset? SubjectsNotifiedAtUtc { get; set; }
    public string? CorrectiveActions { get; set; }
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
