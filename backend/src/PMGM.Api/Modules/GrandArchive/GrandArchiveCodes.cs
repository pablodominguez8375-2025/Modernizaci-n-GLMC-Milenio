namespace PMGM.Api.Modules.GrandArchive;

public static class GrandArchiveCodes
{
    public static class Status
    {
        public const string Active = "active";
        public const string Withdrawn = "withdrawn";
        public static bool IsValid(string value) => value is Active or Withdrawn;
    }

    public static class RecordType
    {
        public const string Decree = "decree";
        public const string Communication = "communication";
        public const string Minute = "minute";
        public const string Resolution = "resolution";
        public const string Regulation = "regulation";
        public const string Correspondence = "correspondence";
        public const string HistoricalRecord = "historical_record";
        public const string Other = "other";

        public static bool IsValid(string value)
            => value is Decree or Communication or Minute or Resolution or Regulation or Correspondence or HistoricalRecord or Other;

        // Publicables a los Hermanos (#653). Correspondencia y registros históricos son internos.
        public static bool IsPublishable(string value)
            => value is Decree or Communication or Minute or Resolution or Regulation or Other;
    }
}

public static class GrandArchivePolicy
{
    /// <summary>Solo el perfil Gran Archivero publica y retira publicaciones: el administrador de Gran Logia y el Máster no (perfiles separados).</summary>
    public static bool IsGrandArchivist(System.Security.Claims.ClaimsPrincipal user)
        => user.Claims.Any(x => x.Type == PMGM.Api.Modules.Authorization.InstitutionalClaims.Scope &&
               string.Equals(x.Value, "order", StringComparison.OrdinalIgnoreCase)) &&
           user.Claims.Any(x => x.Type == PMGM.Api.Modules.Authorization.InstitutionalClaims.Role &&
               string.Equals(x.Value, PMGM.Api.Modules.Authorization.InstitutionalRoles.GrandArchivist, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidDegree(int degree) => degree is >= 1 and <= 3;

    private static readonly HashSet<string> ForbiddenDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "plancha",
        "plancha_de_trabajo",
        "plancha_trabajo",
        "work_paper",
        "workpaper",
        "paper"
    };

    public static bool IsForbiddenWorkPaper(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType)) return false;
        var normalized = documentType.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return ForbiddenDocumentTypes.Contains(normalized) || normalized.Contains("plancha", StringComparison.Ordinal);
    }
}
