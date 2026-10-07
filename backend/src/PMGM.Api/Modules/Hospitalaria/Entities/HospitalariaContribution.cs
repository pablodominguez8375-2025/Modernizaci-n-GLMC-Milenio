using PMGM.Api.Modules.Core.Entities;
namespace PMGM.Api.Modules.Hospitalaria.Entities;

// Aporte por Taller, nunca por miembro. El monto se copia al generar cada período.
public sealed class HospitalariaContributionRate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public decimal Amount { get; set; } = 6000;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
public sealed class HospitalariaContributionObligation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid RateId { get; set; }
    public HospitalariaContributionRate Rate { get; set; } = null!;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public decimal AmountDue { get; set; }
    public string Status { get; set; } = "pending";
    public DateOnly? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    public string? RecordedBySubject { get; set; }
    public string? ReviewedBySubject { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
