using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Ceremonies.Entities;

public sealed class CeremonyRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string CeremonyType { get; set; }
    public Guid? MemberId { get; set; }
    public Member? Member { get; set; }
    public Guid? CandidatePersonId { get; set; }
    public Person? CandidatePerson { get; set; }
    public Guid? AdmissionCaseId { get; set; }
    public DateOnly? ProposedDate { get; set; }
    public required string Status { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class CeremonyValidation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CeremonyRequestId { get; set; }
    public CeremonyRequest CeremonyRequest { get; set; } = null!;
    public required string ValidationType { get; set; }
    public required string Status { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class CandidatePublication
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CeremonyRequestId { get; set; }
    public CeremonyRequest CeremonyRequest { get; set; } = null!;
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public DateTimeOffset PublishedFromUtc { get; set; }
    public DateTimeOffset? PublishedUntilUtc { get; set; }
    public int RequiredDays { get; set; }
    public required string RuleCode { get; set; }
    public required string Status { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class InstitutionalRuleSetting
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Value { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public required string Status { get; set; }
    public string? SourceReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
