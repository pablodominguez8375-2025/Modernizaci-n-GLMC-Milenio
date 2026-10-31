namespace PMGM.Api.Modules.Core;

/// <summary>
/// La organización técnica de soporte NO constituye un Taller institucional.
/// Debe ser la única con tipo support y número 00; el nombre visible nunca autoriza acceso.
/// </summary>
public static class SupportWorkshopPolicy
{
    public const string OrganizationType = "support";
    public const string OrganizationNumber = "00";

    public static bool IsConfiguredSupportWorkshop(string? type, string? number)
        => IsSupportOrganization(type) && string.Equals(number, OrganizationNumber, StringComparison.Ordinal);

    public static bool IsSupportOrganization(string? type)
        => string.Equals(type, OrganizationType, StringComparison.OrdinalIgnoreCase);

    /// <summary>Solo organizaciones reales en catálogos/directorios generales.</summary>
    public static bool IncludeInInstitutionalDirectory(string? type)
        => !IsSupportOrganization(type);
}
