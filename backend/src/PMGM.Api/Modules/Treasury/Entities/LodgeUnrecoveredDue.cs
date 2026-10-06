namespace PMGM.Api.Modules.Treasury.Entities;

// Historical loss evidence; never a cash movement or a payment allocation.
public sealed class LodgeUnrecoveredDue
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid WithdrawalRequestId { get; set; }
    public Guid ChargeId { get; set; }
    public LodgeMemberCharge Charge { get; set; } = null!;
    public DateOnly RecognitionDate { get; set; }
    public decimal ChargedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public required string EvidenceReference { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
