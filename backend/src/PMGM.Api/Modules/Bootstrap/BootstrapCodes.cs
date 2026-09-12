using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Bootstrap;

public static class BootstrapCodes
{
    public static class ApplicationStatus
    {
        public const string Applied = "applied";
    }

    public static class SecurityScope
    {
        public const string Platform = "platform";
        public const string Order = "order";
        public const string Organization = "organization";
    }

    public static class SecurityCategory
    {
        public const string System = "system";
        public const string Administrative = "administrative";
        public const string Support = "support";
    }

    public static class OfficeCategory
    {
        public const string Administrative = "administrative";
        public const string Ritual = "ritual";
    }

    public const string GrandLodgeType = "grand_lodge";
    public const string WorkshopType = "workshop";

    public static readonly IReadOnlyList<SecurityProfileSeed> SecurityProfiles = new[]
    {
        new SecurityProfileSeed(
            InstitutionalRoles.PlatformSuperAdmin,
            "Superadmin de plataforma",
            SecurityScope.Platform,
            SecurityCategory.System,
            "Control total de la plataforma. Rol reservado para administración técnica superior."),
        new SecurityProfileSeed(
            InstitutionalRoles.GranLogiaAdmin,
            "Administrador de Gran Logia",
            SecurityScope.Order,
            SecurityCategory.Administrative,
            "Administra la estructura institucional y las funciones de la Orden dentro de los módulos habilitados."),
        new SecurityProfileSeed(
            InstitutionalRoles.TallerAdmin,
            "Administrador de Taller",
            SecurityScope.Organization,
            SecurityCategory.Administrative,
            "Administra usuarios, configuración y operación de su Taller sin extender permisos a otros Talleres."),
        new SecurityProfileSeed(
            "platform_support_template",
            "Plantilla de soporte de plataforma",
            SecurityScope.Platform,
            SecurityCategory.Support,
            "Base reservada para perfiles secundarios configurables por el Superadmin. No concede permisos por sí sola.")
    };

    public static readonly IReadOnlyList<OfficeSeed> WorkshopOffices = new[]
    {
        new OfficeSeed("worshipful_master", "Venerable Maestro", OfficeCategory.Administrative, true, 10),
        new OfficeSeed("immediate_past_master", "Inmediato Ex Venerable Maestro", OfficeCategory.Administrative, true, 20),
        new OfficeSeed("first_warden", "Primer Vigilante", OfficeCategory.Administrative, true, 30),
        new OfficeSeed("second_warden", "Segundo Vigilante", OfficeCategory.Administrative, true, 40),
        new OfficeSeed("orator", "Orador", OfficeCategory.Administrative, true, 50),
        new OfficeSeed("secretary", "Secretario/a", OfficeCategory.Administrative, true, 60),
        new OfficeSeed("hospitalaria", "Hospitalaria", OfficeCategory.Administrative, true, 70),
        new OfficeSeed("treasurer", "Tesorero/a", OfficeCategory.Administrative, true, 80),
        new OfficeSeed("first_deacon", "Primer Diácono", OfficeCategory.Ritual, false, 110),
        new OfficeSeed("second_deacon", "Segundo Diácono", OfficeCategory.Ritual, false, 120),
        new OfficeSeed("master_of_ceremonies", "Maestro de Ceremonias", OfficeCategory.Ritual, false, 130),
        new OfficeSeed("inner_guard", "Guarda Templo Interno", OfficeCategory.Ritual, false, 140),
        new OfficeSeed("outer_guard", "Guarda Templo Externo", OfficeCategory.Ritual, false, 150, new[] { "Retejador" }),
        new OfficeSeed("master_of_harmony", "Maestro de Armonía", OfficeCategory.Ritual, false, 160)
    };
}

public sealed record SecurityProfileSeed(
    string Code,
    string Name,
    string Scope,
    string Category,
    string Description);

public sealed record OfficeSeed(
    string Code,
    string Name,
    string Category,
    bool IsPrimary,
    int SortOrder,
    IReadOnlyList<string>? Aliases = null);
