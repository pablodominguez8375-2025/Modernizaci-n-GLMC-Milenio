using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Hospitalaria.Entities;

public sealed class HospitalariaMonthlySubmission
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public DateOnly CutoffDate { get; set; }

    // Snapshot agregado: nunca contiene beneficiarios ni observaciones personales.
    public decimal IncomeAmount { get; set; }
    public decimal ApprovedExpenseAmount { get; set; }
    public decimal PeriodNetAmount { get; set; }
    public int MovementCount { get; set; }
    public int PendingExpenseCount { get; set; }

    public decimal ReplenishmentDueAmount { get; set; }
    public decimal ReplenishmentPaidAmount { get; set; }
    public decimal DifferenceAmount { get; set; }
    public string? PaymentReference { get; set; }

    // Revisión mensual del Consejo de Administración (Hospitalaria).
    public Guid? CouncilFinancialReviewId { get; set; }

    public required string Status { get; set; }
    public string? SourceReference { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public string? ReviewedBySubject { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
}
