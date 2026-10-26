namespace PMGM.Api.Modules.RegimenInterior.Entities;

/// <summary>
/// Saldo pendiente de un Taller con Gran Tesorería AL CORTE, informado por oficio de Gran Tesorería
/// y validado/registrado solo por Gran Tesorería o Gran Hospitalaria (append-only; un registro nuevo reemplaza al vigente).
/// No concede sufragio: lo evalúa AssemblyVoterEligibilityPolicy (cualquier monto positivo bloquea).
/// </summary>
public sealed class AssemblyWorkshopBalanceEventRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public Guid OrganizationId { get; set; }
    /// <summary>Responsable que valida el pago: gran_tesoreria (cuotas) o gran_hospitalaria (reposiciones por fallecimiento y cuota anual).</summary>
    public required string Source { get; set; }
    public decimal AmountPending { get; set; }
    public required string Currency { get; set; }
    public required string SourceReference { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateOnly CutoffDate { get; set; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public Guid? SupersedesEventId { get; set; }
}
