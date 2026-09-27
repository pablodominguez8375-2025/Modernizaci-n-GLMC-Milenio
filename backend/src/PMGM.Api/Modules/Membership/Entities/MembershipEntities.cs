using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Membership.Entities;

public sealed class Member
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = null!;
    public string? InstitutionalNumber { get; set; }
    public string? CurrentDegree { get; set; }
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
    public DateOnly? StartDate { get; set; }
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
    public DateOnly? StartDate { get; set; }
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

public sealed class MemberWithdrawalRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid OriginOrganizationId { get; set; }
    public Organization OriginOrganization { get; set; } = null!;
    public required string WithdrawalType { get; set; }
    public DateOnly RequestedEffectiveDate { get; set; }
    public required string Reason { get; set; }
    public required string EvidenceReference { get; set; }
    public required string Status { get; set; }
    public string? Resolution { get; set; }
    public string RequestedBySubject { get; set; } = string.Empty;
    public string? DecidedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? OratorSignatureSubject { get; set; }
    public DateTimeOffset? OratorSignedAtUtc { get; set; }
}

/// <summary>
/// Delegación individual, revocable y de sólo lectura para el Resumen del Taller.
/// La autorización efectiva vuelve a comprobar pertenencia y grado en cada lectura.
/// </summary>
public sealed class LodgeSummaryAccessGrant
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public required string GrantedBySubject { get; set; }
    public DateTimeOffset GrantedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string GrantReason { get; set; }
    public string? RevokedBySubject { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
