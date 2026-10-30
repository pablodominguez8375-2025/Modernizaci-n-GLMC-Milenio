using PMGM.Api.Modules.UserAccounts;

namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// «Ver como un perfil»: el Máster mira la plataforma como la vería un cargo (Tesorero, Secretario…) sin
/// elegir a ninguna persona. Es una identidad sintética de SOLO LECTURA, nunca privilegiada, ligada al
/// Taller 00 de soporte (tipo «support»), de modo que no se exponen datos de Hermanos reales.
/// </summary>
public sealed record SupportViewProfile(string Key, string Label, string Group, string[] Roles, bool OrderScope);

public static class SupportViewProfiles
{
    public const string SubjectPrefix = "profile:";
    public const string Workshop = "Taller";
    public const string GrandLodge = "Gran Logia";

    public static readonly IReadOnlyList<SupportViewProfile> All =
    [
        new("hermano", "Hermano", Workshop, [], false),
        new("venerable", "Venerable Maestro", Workshop, [InstitutionalRoles.TallerVenerable], false),
        new("primer-vigilante", "Primer Vigilante", Workshop, [InstitutionalRoles.TallerPrimerVigilante], false),
        new("segundo-vigilante", "Segundo Vigilante", Workshop, [InstitutionalRoles.TallerSegundoVigilante], false),
        new("tesorero", "Tesorero", Workshop, [InstitutionalRoles.TallerTesoreria], false),
        new("secretario", "Secretario", Workshop, [InstitutionalRoles.TallerSecretaria], false),
        new("hospitalario", "Hospitalario", Workshop, [InstitutionalRoles.TallerHospitalaria], false),
        new("orador", "Orador", Workshop, [InstitutionalRoles.TallerOrador], false),
        new("inmediato-ex-venerable", "Inmediato Ex Venerable Maestro", Workshop, [InstitutionalRoles.TallerInmediatoExVenerable], false),
        new("gran-secretario", "Gran Secretario", GrandLodge, [InstitutionalRoles.GranSecretaria], true),
        new("regimen-interior", "Régimen Interior", GrandLodge, [InstitutionalRoles.RegimenInterior], true),
        new("gran-tesorero", "Gran Tesorero", GrandLodge, [InstitutionalRoles.GranTesoreria], true),
        new("gran-hospitalaria", "Gran Hospitalaria", GrandLodge, [InstitutionalRoles.GranHospitalaria], true),
        new("gran-maestro", "Gran Maestro", GrandLodge, [InstitutionalRoles.GranMaestria], true),
        new("gran-segundo-vigilante", "Gran Segundo Vigilante", GrandLodge, [InstitutionalRoles.GranSegundoVigilante], true),
        new("gran-primer-vigilante", "Gran Primer Vigilante", GrandLodge, [InstitutionalRoles.GranPrimerVigilante], true),
        new("inmediato-ex-gran-maestro", "Inmediato Ex Gran Maestro", GrandLodge, [InstitutionalRoles.InmediatoExGranMaestro], true),
        new("jefatura-docencia", "Jefatura de Docencia", GrandLodge, [InstitutionalRoles.JefaturaDepartamentoDocencia], true),
        new("gran-archivero", "Gran Archivero", GrandLodge, [InstitutionalRoles.GrandArchivist], true),
        new("bibliotecario", "Bibliotecario", GrandLodge, [InstitutionalRoles.Librarian], true),
        new("comunicaciones", "Comunicaciones", GrandLodge, [InstitutionalRoles.Communications], true),
    ];

    public static SupportViewProfile? Find(string? key)
        => All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));

    public static bool IsProfileSubject(string? subject)
        => subject is not null && subject.StartsWith(SubjectPrefix, StringComparison.Ordinal);

    public static SupportViewProfile? FromSubject(string? subject)
        => IsProfileSubject(subject) ? Find(subject![SubjectPrefix.Length..]) : null;

    public static string DisplayName(SupportViewProfile p)
        => p.OrderScope ? p.Label : p.Label + " · Taller 00";

    /// <summary>Identidad sintética del perfil. Los perfiles de Taller solo reciben la organización de soporte.</summary>
    public static AccountClaims Claims(SupportViewProfile p, Guid? supportOrganizationId)
        => new(SubjectPrefix + p.Key, DisplayName(p), "member", true, p.Roles,
            p.OrderScope ? "order" : null,
            supportOrganizationId is Guid org && !p.OrderScope ? [org.ToString()] : []);

    /// <summary>
    /// Todos los perfiles (de Taller y de Gran Logia): toda lectura (GET); el Máster ve todo (PO, 27-10-2026).
    /// Perfiles de Taller: identidad limitada al Taller 00. Perfiles de Gran Logia: alcance de la Orden.
    /// Solo lectura, con motivo, 30 minutos y auditoría con el operador real. Quedan fuera las descargas y
    /// exportaciones (CSV, PDF, DOCX, padrón oficial), que pueden contener datos personales completos.
    /// </summary>
    public static bool AllowsRoute(SupportViewProfile p, string? routePattern)
        => !string.IsNullOrEmpty(routePattern) && !IsDownload(routePattern);

    private static readonly string[] DownloadMarkers =
        ["export", ".csv", ".pdf", ".docx", "padron-oficial", "template-csv", "descarg", "download", "/inhabilitados"];

    public static bool IsDownload(string routePattern)
        => DownloadMarkers.Any(m => routePattern.Contains(m, StringComparison.OrdinalIgnoreCase));
}
