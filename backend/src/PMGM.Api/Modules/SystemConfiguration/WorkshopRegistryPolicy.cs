using System.Text;

namespace PMGM.Api.Modules.SystemConfiguration;

public static class WorkshopRegistryActions
{
    public const string Delete = "delete";
    public const string Retire = "retire";
    public const string Reactivate = "reactivate";
    public static bool IsKnown(string? action) => action is Delete or Retire or Reactivate;
}

public static class WorkshopRegistryCodes
{
    public const string Active = "active";
    public const string Retired = "retired";
    /// <summary>Solo informes por fecha de corte: historia no conocida (nunca se infiere activo).</summary>
    public const string Unknown = "unknown";
    public const string GrandLodgeType = "grand_lodge";
    public const string SupportType = "support";
    public const int ReasonMinLength = 10;
    public const int ReasonMaxLength = 500;
    public static readonly string[] Origins = ["initial_load", "qa_seed", "bootstrap", "other"];
    public static string NormalizeOrigin(string? origin) => Array.IndexOf(Origins, origin) >= 0 ? origin! : "other";
    /// <summary>Gran Logia y soporte son configuración inicial aunque la columna diga «other» (sin UPDATE en la migración).</summary>
    public static string DisplayOrigin(string? origin, string? type)
    {
        var normalized = NormalizeOrigin(origin);
        var technical = string.Equals(type, GrandLodgeType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, SupportType, StringComparison.OrdinalIgnoreCase);
        return technical && normalized == "other" ? "bootstrap" : normalized;
    }
}

public enum WorkshopRegistryDecisionCode
{
    Allowed,
    UnknownAction,
    NotMaster,
    Protected,
    InvalidReason,
    ConfirmationMismatch,
    HasData,
    NotActive,
    NotRetired,
    AlreadyRetired,
    RetireNotEnabled
}

public sealed record WorkshopRegistryFacts(Guid Id, string? Number, string Type, string Status, bool HasData)
{
    /// <summary>Id fijo del Taller 00 de soporte (migración #812).</summary>
    public static readonly Guid SupportWorkshopId = Guid.Parse("b12c0000-0000-4000-8000-000000000000");

    /// <summary>
    /// Protegidos: Gran Logia, organización técnica de soporte (por tipo, por su Id fijo) y CUALQUIER organización
    /// con número «00», sea cual sea su tipo (nunca puede ser un Taller real que se elimine o se dé de baja).
    /// </summary>
    public bool Protected => string.Equals(Type, WorkshopRegistryCodes.GrandLodgeType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, WorkshopRegistryCodes.SupportType, StringComparison.OrdinalIgnoreCase)
        || Id == SupportWorkshopId
        || string.Equals((Number ?? string.Empty).Trim(), "00", StringComparison.Ordinal);
    public string ShortCode => Id.ToString("N")[..8].ToUpperInvariant();
    /// <summary>Texto que el Máster escribe para confirmar: el número, o el código corto si no tiene.</summary>
    public string ConfirmationText => string.IsNullOrWhiteSpace(Number) ? ShortCode : Number!;
}

/// <summary>
/// Reglas del PO (09-11-2026): solo el Máster elimina, da de baja y reactiva.
/// Eliminar exige que no haya datos asociados; con datos solo se da de baja.
/// La Gran Logia y el Taller 00 de soporte son protegidos. La UI nunca decide: el servidor valida todo.
/// </summary>
public static class WorkshopRegistryPolicy
{
    /// <param name="retireEnabled">
    /// «Dar de baja» solo marca el registro: los demás módulos aún no filtran ni bloquean escrituras sobre un Taller de baja.
    /// Hasta que exista esa cobertura transversal (PR aparte), la baja queda bloqueada de forma explícita.
    /// </param>
    public static WorkshopRegistryDecisionCode Decide(string? action, bool isMaster, WorkshopRegistryFacts facts, string? reason, string? confirmation, bool retireEnabled)
    {
        if (!WorkshopRegistryActions.IsKnown(action)) return WorkshopRegistryDecisionCode.UnknownAction;
        if (!isMaster) return WorkshopRegistryDecisionCode.NotMaster;
        if (facts.Protected) return WorkshopRegistryDecisionCode.Protected;
        var length = (reason ?? string.Empty).Trim().Length;
        if (length < WorkshopRegistryCodes.ReasonMinLength || length > WorkshopRegistryCodes.ReasonMaxLength)
            return WorkshopRegistryDecisionCode.InvalidReason;
        if (action != WorkshopRegistryActions.Reactivate
            && !string.Equals((confirmation ?? string.Empty).Trim(), facts.ConfirmationText, StringComparison.Ordinal))
            return WorkshopRegistryDecisionCode.ConfirmationMismatch;
        switch (action)
        {
            case WorkshopRegistryActions.Delete:
                if (facts.Status != WorkshopRegistryCodes.Active) return WorkshopRegistryDecisionCode.NotActive;
                return facts.HasData ? WorkshopRegistryDecisionCode.HasData : WorkshopRegistryDecisionCode.Allowed;
            case WorkshopRegistryActions.Retire:
                if (!retireEnabled) return WorkshopRegistryDecisionCode.RetireNotEnabled;
                return facts.Status == WorkshopRegistryCodes.Active ? WorkshopRegistryDecisionCode.Allowed : WorkshopRegistryDecisionCode.AlreadyRetired;
            default:
                return facts.Status == WorkshopRegistryCodes.Retired ? WorkshopRegistryDecisionCode.Allowed : WorkshopRegistryDecisionCode.NotRetired;
        }
    }
}

public sealed record WorkshopCsvRow(Guid Id, string ShortCode, string? Number, string Name, string Type, string? OrienteCode,
    string? City, string Origin, string Status, bool HasData, DateTimeOffset CreatedAtUtc);

/// <summary>CSV del registro: «;», UTF-8 con BOM, CRLF y celdas protegidas contra fórmulas. Igual al de la demo.</summary>
public static class WorkshopRegistryCsv
{
    private static readonly string[] Header =
    [
        "Código interno", "Número", "Nombre", "Tipo", "Oriente", "Ciudad", "Origen", "Estado",
        "Tiene datos asociados", "Creado (UTC)", "Identificador"
    ];

    public static string OriginLabel(string origin) => origin switch
    {
        "initial_load" => "Carga inicial",
        "qa_seed" => "Prueba (creado por el sistema QA)",
        "bootstrap" => "Configuración inicial",
        _ => "Otro"
    };

    public static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny(['"', ';', '\n', '\r']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    public static byte[] Build(IEnumerable<WorkshopCsvRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append('﻿');
        builder.Append(string.Join(';', Header.Select(Cell))).Append("\r\n");
        foreach (var row in rows)
        {
            string[] cells =
            [
                row.ShortCode, row.Number ?? string.Empty, row.Name,
                string.Equals(row.Type, WorkshopRegistryCodes.GrandLodgeType, StringComparison.OrdinalIgnoreCase) ? "Gran Logia" : "Taller",
                row.OrienteCode ?? string.Empty, row.City ?? string.Empty, OriginLabel(row.Origin),
                row.Status == WorkshopRegistryCodes.Retired ? "De baja" : "Activo",
                row.HasData ? "Sí" : "No",
                row.CreatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
                row.Id.ToString()
            ];
            builder.Append(string.Join(';', cells.Select(Cell))).Append("\r\n");
        }
        // El BOM ya está como primer carácter; se codifica sin agregar otro.
        return new UTF8Encoding(false).GetBytes(builder.ToString());
    }
}
