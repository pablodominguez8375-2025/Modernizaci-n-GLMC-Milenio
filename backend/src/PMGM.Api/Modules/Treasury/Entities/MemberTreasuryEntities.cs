namespace PMGM.Api.Modules.Treasury.Entities;

public sealed class MemberCharge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string Concept { get; set; } = string.Empty;
    public string? Period { get; set; }
    public string ChargeType { get; set; } = MemberTreasuryCodes.ChargeType.OrdinaryDue;
    public DateOnly IssuedDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = MemberTreasuryCodes.Currency.Clp;
    public string Status { get; set; } = MemberTreasuryCodes.ChargeStatus.Open;
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBySubject { get; set; } = string.Empty;
}

public sealed class MemberPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = MemberTreasuryCodes.Currency.Clp;
    public string Method { get; set; } = MemberTreasuryCodes.PaymentMethod.Other;
    public string? ReceiptNumber { get; set; }
    public Guid? ReceiptDocumentId { get; set; }
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string RecordedBySubject { get; set; } = string.Empty;
}

public sealed class MemberPaymentAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public Guid ChargeId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
