namespace PMGM.Api.Modules.Core;

/// <summary>
/// La organización sintética de soporte NO constituye un Taller institucional.
/// La identificación es por tipo, nunca por texto de nombre o número 00.
/// </summary>
public static class SupportWorkshopPolicy
{
    public const string OrganizationType = "support";

    public static bool IsSupportOrganization(string? type)
        => string.Equals(type, OrganizationType, StringComparison.OrdinalIgnoreCase);

    /// <summary>Solo organizaciones reales en catálogos/directorios generales.</summary>
    public static bool IncludeInInstitutionalDirectory(string? type)
        => !IsSupportOrganization(type);
}
