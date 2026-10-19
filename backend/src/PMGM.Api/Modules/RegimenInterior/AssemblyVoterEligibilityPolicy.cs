namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Reglas del padrón de Gran Asamblea. No crea electores: exige que las
/// evidencias institucionales provengan de servicios autorizados del backend.
/// Ningún componente de UI puede fabricar saldos o actos de Régimen Interior.
/// </summary>
public static class AssemblyVoterEligibilityPolicy
{
    public const string GrandTreasurySource = "grand_treasury";
    public const string RegimenInteriorAuthority = "regimen_interior";

    public const string Eligible = "habilitado";
    public const string Ineligible = "inhabilitado";
    public const string Pending = "pendiente";
    public const string AttendanceOnly = "solo_asistencia";

    public static AssemblyEligibilityDecision Evaluate(AssemblyEligibilityFacts facts)
    {
        if (!facts.IsAssemblyMember || facts.MemberActive == false ||
            facts.InstitutionalRightsActive == false || facts.HonorAllowsAttendance == false)
            return new AssemblyEligibilityDecision(Ineligible, false, false, "DERECHOS_O_CALIDAD_INVALIDOS");

        // Desconocido no es un permiso: el fallo de integración nunca habilita.
        if (facts.MemberActive is null || facts.InstitutionalRightsActive is null ||
            facts.HonorAllowsAttendance is null || facts.RegimenAllowsAttendance is null)
            return new AssemblyEligibilityDecision(Pending, false, false, "EVIDENCIA_DERECHOS_PENDIENTE");

        if (facts.RegimenAllowsAttendance == false)
            return new AssemblyEligibilityDecision(Ineligible, false, false, "RESTRICCION_ASISTENCIA");

        // La habilitación de asistencia se decide con independencia del sufragio.
        if (facts.HonorAllowsSuffrage == false || facts.RegimenAllowsSuffrage == false)
            return new AssemblyEligibilityDecision(AttendanceOnly, true, false, "RESTRICCION_SUFRAGIO");

        if (facts.HonorAllowsSuffrage is null || facts.RegimenAllowsSuffrage is null)
            return new AssemblyEligibilityDecision(Pending, true, false, "EVIDENCIA_SUFRAGIO_PENDIENTE");

        var balance = facts.GrandTreasuryBalance;
        if (balance is null || balance.OrganizationId != facts.OrganizationId ||
            balance.AsOfDate != facts.Cutoff ||
            balance.SourceSystem != GrandTreasurySource ||
            !balance.Reconciled || string.IsNullOrWhiteSpace(balance.SourceReference) ||
            string.IsNullOrWhiteSpace(balance.Currency) || balance.AmountPending < 0m)
            return new AssemblyEligibilityDecision(Pending, true, false, "SALDO_GRAN_TESORERIA_NO_ACREDITADO");

        // Regla del PO: cualquier monto positivo, sin umbral ni gracia.
        if (balance.AmountPending > 0m)
        {
            var exception = facts.DebtException;
            if (exception is null || !IsEffectiveException(exception, facts.OrganizationId, facts.Cutoff))
                return new AssemblyEligibilityDecision(AttendanceOnly, true, false, "MOROSIDAD_GRAN_TESORERIA");
        }

        return new AssemblyEligibilityDecision(Eligible, true, true, "HABILITACION_VERIFICADA");
    }

    public static bool IsEffectiveException(AssemblyDebtException exception, Guid organizationId, DateOnly cutoff) =>
        exception.OrganizationId == organizationId &&
        exception.Authority == RegimenInteriorAuthority &&
        exception.DecidedAt <= cutoff &&
        (exception.RevokedAt is null || exception.RevokedAt.Value > cutoff) &&
        !string.IsNullOrWhiteSpace(exception.DecidedBy) &&
        !string.IsNullOrWhiteSpace(exception.Reason) &&
        !string.IsNullOrWhiteSpace(exception.EvidenceReference);

    // Debe comprobarse con el permiso RBAC de Régimen Interior en el endpoint,
    // no con el texto de un rol suministrado por el navegador.
    public static bool CanDecideDebtException(string confirmedInstitutionalRole) =>
        string.Equals(confirmedInstitutionalRole, RegimenInteriorAuthority, StringComparison.Ordinal);
}

public sealed record AssemblyGrandTreasuryBalance(
    Guid OrganizationId,
    DateOnly AsOfDate,
    string SourceSystem,
    string SourceReference,
    string Currency,
    decimal AmountPending,
    bool Reconciled);

public sealed record AssemblyDebtException(
    Guid OrganizationId,
    string Authority,
    string DecidedBy,
    DateOnly DecidedAt,
    string Reason,
    string EvidenceReference,
    DateOnly? RevokedAt);

public sealed record AssemblyEligibilityFacts(
    Guid OrganizationId,
    DateOnly Cutoff,
    bool IsAssemblyMember,
    bool? MemberActive,
    bool? InstitutionalRightsActive,
    bool? HonorAllowsAttendance,
    bool? RegimenAllowsAttendance,
    bool? HonorAllowsSuffrage,
    bool? RegimenAllowsSuffrage,
    AssemblyGrandTreasuryBalance? GrandTreasuryBalance,
    AssemblyDebtException? DebtException);

public sealed record AssemblyEligibilityDecision(
    string Status,
    bool MayAttend,
    bool MayVote,
    string ReasonCode);
