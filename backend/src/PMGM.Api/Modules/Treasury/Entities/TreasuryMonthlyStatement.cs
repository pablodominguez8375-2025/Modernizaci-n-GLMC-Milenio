using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Treasury.Entities;

public sealed class TreasuryMonthlyStatement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public DateOnly CutoffDate { get; set; }
    public required string Status { get; set; }
    public string? SourceReference { get; set; }
    public Guid? RectifiesStatementId { get; set; }
    public TreasuryMonthlyStatement? RectifiesStatement { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? ReconciledAtUtc { get; set; }
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
    public Membership? Membership { get; set; }
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
