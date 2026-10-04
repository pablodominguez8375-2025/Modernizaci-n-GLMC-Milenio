using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using MembershipEntity = PMGM.Api.Modules.Membership.Entities.Membership;

namespace PMGM.Api.Modules.Treasury.Entities;

public sealed class TreasuryMonthlyStatement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string Currency { get; set; } = "CLP";
    public DateOnly CutoffDate { get; set; }
    public required string Status { get; set; }
    public string? SourceReference { get; set; }
    public Guid? RectifiesStatementId { get; set; }
    public TreasuryMonthlyStatement? RectifiesStatement { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? ReconciledAtUtc { get; set; }
    public string? BankReceiptConfirmedBySubject { get; set; }
    public DateTimeOffset? BankReceiptConfirmedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public ICollection<TreasuryMonthlyStatementLine> Lines { get; set; } = new List<TreasuryMonthlyStatementLine>();
    public ICollection<TreasuryPayment> Payments { get; set; } = new List<TreasuryPayment>();
}

public sealed class TreasuryMonthlyStatementLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid StatementId { get; set; }
    public TreasuryMonthlyStatement Statement { get; set; } = null!;
    public Guid? MemberId { get; set; }
    public Member? Member { get; set; }
    public Guid? MembershipId { get; set; }
    public MembershipEntity? Membership { get; set; }
    public required string DegreeCodeAtCutoff { get; set; }
    public string? OfficeCodeAtCutoff { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public string? AdjustmentType { get; set; }
    public string? AuthorizationReference { get; set; }
    public string? Observation { get; set; }
    public required string IdentityMatchStatus { get; set; }
    public decimal PayableAmount => BaseAmount + AdjustmentAmount;
}

public sealed class TreasuryPayment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid StatementId { get; set; }
    public TreasuryMonthlyStatement Statement { get; set; } = null!;
    public required string PaymentMethod { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public required string PayerDisplayName { get; set; }
    public string? PayerRut { get; set; }
    public string? Reference { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class TreasuryAdjustment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public required string AdjustmentType { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public decimal Amount { get; set; }
    public required string AuthorizationReference { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeFeePlan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string FeeType { get; set; }
    public decimal MemberAmount { get; set; }
    public decimal GrandTreasuryAmount { get; set; }
    public Guid? TariffVersionId { get; set; }
    public string Currency { get; set; } = "CLP";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeMemberCharge
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid FeePlanId { get; set; }
    public LodgeFeePlan FeePlan { get; set; } = null!;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public decimal MemberAmount { get; set; }
    public decimal GrandTreasuryAmount { get; set; }
    public Guid? TariffVersionId { get; set; }
    public string Currency { get; set; } = "CLP";
    public required string Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<LodgeMemberPayment> Payments { get; set; } = new List<LodgeMemberPayment>();
    public ICollection<LodgeMemberPaymentAllocation> Allocations { get; set; } = new List<LodgeMemberPaymentAllocation>();
}

public sealed class LodgeMemberReceipt
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public required string PaymentMethod { get; set; }
    public DateOnly PaymentDate { get; set; }
    public required string ReceiptNumber { get; set; }
    public required string IdempotencyKey { get; set; }
    public string? Reference { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public ICollection<LodgeReceiptAdjustment> Adjustments { get; set; } = new List<LodgeReceiptAdjustment>();
    public ICollection<LodgeMemberPaymentAllocation> Allocations { get; set; } = new List<LodgeMemberPaymentAllocation>();
}

public sealed class LodgeMemberPaymentAllocation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public LodgeMemberReceipt Receipt { get; set; } = null!;
    public Guid ChargeId { get; set; }
    public LodgeMemberCharge Charge { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public Guid? AdjustmentId { get; set; }
    public LodgeReceiptAdjustment? Adjustment { get; set; }
    public Guid? ReversesAllocationId { get; set; }
    public required string AllocatedBySubject { get; set; }
    public DateTimeOffset AllocatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeMemberPayment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ChargeId { get; set; }
    public LodgeMemberCharge Charge { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public required string PaymentMethod { get; set; }
    public DateOnly PaymentDate { get; set; }
    public required string ReceiptNumber { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? Reference { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeHospitalariaMovement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string MovementType { get; set; }
    public required string Category { get; set; }
    public decimal Amount { get; set; }
    public DateOnly MovementDate { get; set; }
    public string? MemberReference { get; set; }
    public string? Destination { get; set; }
    public string? EvidenceReference { get; set; }
    public string? Observation { get; set; }
    public required string ApprovalStatus { get; set; }
    public string? ApprovalSource { get; set; }
    public Guid? CouncilDecisionId { get; set; }
    public string? ApprovedBySubject { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeTreasuryExpense
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string Category { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public DateOnly ExpenseDate { get; set; }
    public required string Description { get; set; }
    public string? EvidenceReference { get; set; }
    public required string ApprovalStatus { get; set; }
    public required string RecordedBySubject { get; set; }
    public string? ApprovedBySubject { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeTreasuryIncome
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string Category { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CLP";
    public DateOnly IncomeDate { get; set; }
    public required string Description { get; set; }
    public string? EvidenceReference { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeTreasuryConfiguration
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public decimal OpeningBalance { get; set; }
    public string Currency { get; set; } = "CLP";
    public DateOnly OpeningBalanceDate { get; set; }
    public required string IncomeCategories { get; set; }
    public required string ExpenseCategories { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeTreasuryYearClosure
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int AccountingYear { get; set; }
    public string Currency { get; set; } = "CLP";
    public decimal OpeningBalance { get; set; }
    public decimal Income { get; set; }
    public decimal AuthorizedExpenses { get; set; }
    public decimal ClosingBalance { get; set; }
    public int MovementCount { get; set; }
    public required string ClosedBySubject { get; set; }
    public DateTimeOffset ClosedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeTreasuryReconciliation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string Currency { get; init; } = "CLP";
    public decimal OpeningBalance { get; init; }
    public decimal Income { get; init; }
    public decimal AuthorizedExpenses { get; init; }
    public decimal PendingExpenses { get; init; }
    public decimal ClosingBalance { get; init; }
    public decimal ObservedBalance { get; init; }
    public decimal Difference { get; init; }
    public int MovementCount { get; init; }
    public string? EvidenceReference { get; init; }
    public string? Notes { get; init; }
    public required string RecordedBySubject { get; init; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LodgeReceiptAdjustment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public LodgeMemberReceipt Receipt { get; set; } = null!;
    public required string Kind { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public decimal CashAmount { get; set; }
    public required string Reason { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestPayload { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
