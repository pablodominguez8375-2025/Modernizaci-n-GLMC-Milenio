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
        new("gran-secretario", "Gran Secretario", GrandLodge, [InstitutionalRoles.GranSecretaria], true),
        new("regimen-interior", "Régimen Interior", GrandLodge, [InstitutionalRoles.RegimenInterior], true),
        new("gran-tesorero", "Gran Tesorero", GrandLodge, [InstitutionalRoles.GranTesoreria], true),
        new("gran-maestro", "Gran Maestro", GrandLodge, [InstitutionalRoles.GranMaestria], true),
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
    /// Perfiles de Taller: toda lectura (GET) con identidad limitada al Taller 00, que no contiene Hermanos reales.
    /// Perfiles de Gran Logia: tienen alcance de toda la Orden, por eso solo la lista cerrada de rutas sin datos personales.
    /// </summary>
    public static bool AllowsRoute(SupportViewProfile p, string? routePattern)
        => !p.OrderScope ? !string.IsNullOrEmpty(routePattern) : SupportViewAsAllowlist.Allows(routePattern);
}
