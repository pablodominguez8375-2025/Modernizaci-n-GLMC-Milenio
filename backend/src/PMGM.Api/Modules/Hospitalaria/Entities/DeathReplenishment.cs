using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Hospitalaria.Entities;

public sealed class HospitalariaReplenishmentRate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public decimal AmountPerActiveMember { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public required string SourceReference { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DeathReplenishmentCase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid DeathStatusEventId { get; set; }
    public Guid DeceasedMemberId { get; set; }
    public Member DeceasedMember { get; set; } = null!;
    public DateOnly DeathDate { get; set; }
    public decimal AmountPerActiveMember { get; set; }
    public required string Status { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<DeathReplenishmentObligation> Obligations { get; set; } = new List<DeathReplenishmentObligation>();
}

public sealed class DeathReplenishmentObligation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public DeathReplenishmentCase Case { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int SubmissionNumber { get; set; } = 1;
    public Guid MembershipId { get; set; }
    public Membership Membership { get; set; } = null!;
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public decimal AmountDue { get; set; }
    public required string Status { get; set; }
    public ICollection<DeathReplenishmentPayment> Payments { get; set; } = new List<DeathReplenishmentPayment>();
}

public sealed class DeathReplenishmentPayment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ObligationId { get; set; }
    public DeathReplenishmentObligation Obligation { get; set; } = null!;
    public decimal Amount { get; set; }
    public required string PaymentMethod { get; set; }
    public DateOnly PaymentDate { get; set; }
    public required string ReceiptNumber { get; set; }
    public required string Reference { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DeathReplenishmentTransfer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CaseId { get; set; }
    public DeathReplenishmentCase Case { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly TransferDate { get; set; }
    public required string Reference { get; set; }
    public required string Status { get; set; }
    public string? ReviewedBySubject { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
