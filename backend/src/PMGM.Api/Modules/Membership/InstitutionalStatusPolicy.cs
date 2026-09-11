namespace PMGM.Api.Modules.Membership;

/// <summary>
/// Reglas transversales para interpretar el último estado institucional de un Hermano.
/// Centraliza la diferencia entre Past Activo y Retiro voluntario / En sueño para evitar
/// que reportes, Tesorería y futuras automatizaciones vuelvan a equiparar ambas figuras.
/// </summary>
public static class InstitutionalStatusPolicy
{
    public static bool IsPastActive(string? institutionalStatus)
        => Is(institutionalStatus, MembershipCodes.InstitutionalStatus.PastActive);

    public static bool IsVoluntaryWithdrawal(string? institutionalStatus)
        => Is(institutionalStatus, MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal);

    /// <summary>
    /// Indica si el estado institucional conserva al Hermano en el cuadro del Taller.
    /// Past Activo conserva pertenencia; En sueño no constituye vínculo operativo vigente.
    /// </summary>
    public static bool KeepsWorkshopRosterMembership(string? institutionalStatus)
        => !Is(institutionalStatus, MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal) &&
           !Is(institutionalStatus, MembershipCodes.InstitutionalStatus.ForcedWithdrawal) &&
           !Is(institutionalStatus, MembershipCodes.InstitutionalStatus.Deceased);

    /// <summary>
    /// Activo operativo para labores regulares. Past Activo permanece en el cuadro,
    /// pero no se contabiliza como activo operacional.
    /// </summary>
    public static bool IsOperationallyActive(string? institutionalStatus)
        => Is(institutionalStatus, MembershipCodes.InstitutionalStatus.Active) ||
           Is(institutionalStatus, MembershipCodes.InstitutionalStatus.Reinstated);

    /// <summary>
    /// Regla que debe usar el generador de obligaciones ordinarias: sólo genera cuotas
    /// cuando existe membresía vigente y el estado institucional es operativo.
    /// De este modo Past Activo y Retiro voluntario / En sueño nunca generan nuevas cuotas.
    /// Las obligaciones históricas se conservan por separado.
    /// </summary>
    public static bool GeneratesOrdinaryDues(string? institutionalStatus, bool hasActiveMembership)
        => hasActiveMembership && IsOperationallyActive(institutionalStatus);

    private static bool Is(string? value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
