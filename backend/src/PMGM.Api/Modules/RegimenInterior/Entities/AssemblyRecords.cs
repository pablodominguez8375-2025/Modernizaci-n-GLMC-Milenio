namespace PMGM.Api.Modules.RegimenInterior.Entities;

/// <summary>
/// Corte de Gran Asamblea. Un registro creado no implica habilitación electoral.
/// Un cierre futuro debe ser transaccional, auditable e inmutable.
/// </summary>
public sealed class AssemblyRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public DateOnly MeetingDate { get; set; }
    public DateOnly CutoffDate { get; set; }
    public string Status { get; set; } = "draft";
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosedBySubject { get; set; }
    public string? ClosureEvidenceDigest { get; set; }
    public ICollection<AssemblyMemberStatusRecord> Members { get; set; } = new List<AssemblyMemberStatusRecord>();
}

/// <summary>
/// Calidad histórica/electoral acreditada para una Asamblea y un Taller,
/// con los identificadores de fuente, no una simple inferencia de EndDate.
/// </summary>
public sealed class AssemblyMemberStatusRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public AssemblyRecord Assembly { get; set; } = null!;
    public Guid PersonId { get; set; }
    public Guid MemberId { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Category { get; set; }
    public Guid SourceOfficeAssignmentId { get; set; }
    public required string SourceEvidenceReference { get; set; }
    public bool FullTermVerified { get; set; }
    public required string LastNamesAtCutoff { get; set; }
    public required string FirstNamesAtCutoff { get; set; }
    public required string RutAtCutoff { get; set; }
    public required string WorkshopAtCutoff { get; set; }
    public DateTimeOffset VerifiedAtUtc { get; set; }
    public ICollection<AssemblyEligibilityRecord> EligibilityHistory { get; set; } = new List<AssemblyEligibilityRecord>();
}

/// <summary>
/// Evaluación documentada; no representa un voto individual ni su sentido.
/// Las revisiones se agregan como nuevas filas (historial conservado).
/// </summary>
public sealed class AssemblyEligibilityRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public Guid MemberStatusId { get; set; }
    public AssemblyMemberStatusRecord MemberStatus { get; set; } = null!;
    public required string Status { get; set; }
    public bool MayAttend { get; set; }
    public bool MayVote { get; set; }
    public required string ReasonCode { get; set; }
    public decimal? GrandTreasuryPendingAmount { get; set; }
    public string? GrandTreasuryCurrency { get; set; }
    public string? GrandTreasurySourceReference { get; set; }
    public DateOnly? GrandTreasuryAsOfDate { get; set; }
    public string? InstitutionalRightsSourceReference { get; set; }
    public string? HonorRightsSourceReference { get; set; }
    public required string EvaluatedBySubject { get; set; }
    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public Guid? SupersedesEligibilityId { get; set; }
}

/// <summary>
/// Evento append-only del órgano competente: 'lift' o 'revoke'.
/// No reemplaza la validación RBAC ni el registro general de auditoría.
/// </summary>
public sealed class AssemblyDebtDecisionEventRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public Guid OrganizationId { get; set; }
    public required string Action { get; set; }
    public required string Authority { get; set; }
    public required string DecidedBySubject { get; set; }
    public required string Reason { get; set; }
    public required string EvidenceReference { get; set; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public Guid? ReversesEventId { get; set; }
}

/// <summary>
/// Efectos institucionales mínimos. No se guardan detalles de expedientes
/// reservados del Tribunal de Honor dentro del padrón de firmas.
/// </summary>
public sealed class InstitutionalRestrictionRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public required string SourceAuthority { get; set; }
    public required string RestrictionType { get; set; }
    public bool AffectsAttendance { get; set; }
    public bool AffectsSuffrage { get; set; }
    public bool AffectsInstitutionalRights { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public required string EvidenceReference { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
