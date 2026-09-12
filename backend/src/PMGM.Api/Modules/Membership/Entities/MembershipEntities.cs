using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Membership.Entities;

public sealed class Member
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string? InstitutionalNumber { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class Membership
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string MembershipType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public required string Status { get; set; }
    public string? EndReason { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class InstitutionalStatusEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public required string EventType { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string? EvidenceReference { get; set; }
    public string? Notes { get; set; }
}

public sealed class DegreeEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string Degree { get; set; }
    public required string EventType { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? EvidenceReference { get; set; }
}

public sealed class OfficeAssignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string OfficeType { get; set; }
    public required string Period { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class MemberTransfer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid SourceMembershipId { get; set; }
    public Membership SourceMembership { get; set; } = null!;
    public Guid SourceOrganizationId { get; set; }
    public Organization SourceOrganization { get; set; } = null!;
    public Guid TargetOrganizationId { get; set; }
    public Organization TargetOrganization { get; set; } = null!;
    public Guid? TargetMembershipId { get; set; }
    public Membership? TargetMembership { get; set; }
    public DateOnly RequestedDate { get; set; }
    public DateOnly ProposedEffectiveDate { get; set; }
    public DateOnly? ApprovedEffectiveDate { get; set; }
    public required string Status { get; set; }
    public string? Reason { get; set; }
    public string? Resolution { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTimeOffset? ExecutedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
