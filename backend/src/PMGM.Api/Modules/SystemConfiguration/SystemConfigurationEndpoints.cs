using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.SystemConfiguration;

public static class SystemConfigurationEndpoints
{
    private static readonly IReadOnlyDictionary<string, Definition> Definitions = new Dictionary<string, Definition>(StringComparer.OrdinalIgnoreCase)
    {
        ["system.workflow.initiation.approval_steps"] = new("Flujos", "Aprobaciones de iniciación", "text", "Régimen Interior|Gran Tesorería|Gran Hospitalaria|Gran Secretaría|Gran Maestría"),
        ["system.publication.candidate.minimum_days"] = new("Publicaciones", "Días mínimos de publicación", "integer", "20"),
        ["system.interviews.minimum_count"] = new("Procesos", "Entrevistas mínimas", "integer", "3"),
        ["system.rejection.block_months"] = new("Procesos", "Meses de bloqueo tras rechazo", "integer", "12"),
        ["system.library.document_types"] = new("Biblioteca", "Tipos de publicación", "list", "Plancha|Libro|Revista|Ritual|Historia|Circular"),
        ["system.library.catalog_fields"] = new("Biblioteca", "Campos de catalogación", "list", "Autor|Título|Grado|Tema|Fecha|Palabras clave"),
        ["system.archive.document_series"] = new("Gran Archivo", "Series documentales", "list", "Decretos|Actas Gran Asamblea|Correspondencia|Patrimonio histórico"),
        ["system.archive.retention_policy"] = new("Gran Archivo", "Política de conservación", "text", "Según tabla de retención institucional vigente"),
        ["system.documents.allowed_extensions"] = new("Gestor documental", "Extensiones permitidas", "list", "pdf|docx|xlsx|jpg|png"),
        ["system.documents.maximum_size_mb"] = new("Gestor documental", "Tamaño máximo por archivo (MB)", "integer", "50"),
        ["system.treasury.cutoff_day"] = new("Tesorería", "Día de corte mensual", "integer", "5"),
        ["system.hospitalaria.replacement_days"] = new("Hospitalaria", "Plazo de reposición (días)", "integer", "30"),
        ["system.notifications.reminder_days"] = new("Notificaciones", "Anticipación de recordatorios (días)", "integer", "3"),
        ["system.security.session_minutes"] = new("Seguridad", "Duración de sesión (minutos)", "integer", "30"),
        ["system.permissions.system_administrators"] = new("Perfiles y permisos", "Administradores habilitados", "list", "Superadministrador|Administrador Gran Logia"),
        ["system.permissions.lodge_venerable"] = new("Perfiles y permisos", "Venerable Maestro", "list", "Gestión del Taller|Insinuados|Circuito de iniciación|Aprobar egresos|Firmar documentos|Ceremonias"),
        ["system.permissions.lodge_secretariat"] = new("Perfiles y permisos", "Secretaría del Taller", "list", "Datos administrativos|Tenidas y asistencia|Actas|Insinuados|Documentos"),
        ["system.permissions.lodge_treasury"] = new("Perfiles y permisos", "Tesorería del Taller", "list", "Planes de cuota|Ingresos|Egresos|Estado de pagos|Reportes de Tesorería"),
        ["system.permissions.lodge_hospitalaria"] = new("Perfiles y permisos", "Hospitalaria del Taller", "list", "Aportes|Egresos|Reposiciones|Estado de obligaciones|Reportes de Hospitalaria"),
        ["system.permissions.lodge_orator"] = new("Perfiles y permisos", "Orador del Taller", "list", "Revisión normativa|Firmar retiros|Firmar documentos definidos|Consulta de actas"),
        ["system.permissions.lodge_first_warden"] = new("Perfiles y permisos", "Primer Vigilante", "list", "Docencia de Compañeros|Seguimiento formativo|Evaluaciones docentes"),
        ["system.permissions.lodge_second_warden"] = new("Perfiles y permisos", "Segundo Vigilante", "list", "Docencia de Aprendices|Seguimiento formativo|Evaluaciones docentes"),
        ["system.permissions.lodge_past_master"] = new("Perfiles y permisos", "Ex Venerable Maestro", "list", "Apoyo docente|Consulta histórica|Acompañamiento al Taller"),
        ["system.permissions.grand_dignitaries"] = new("Perfiles y permisos", "Grandes dignatarios", "list", "Régimen Interior|Gran Tesorería|Gran Hospitalaria|Gran Secretaría|Gran Archivo|Gran Maestría")
    };

    public static IEndpointRouteBuilder MapSystemConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/system/settings").WithTags("Sistema — Parametrización").RequireAuthorization();
        group.MapGet("/", GetAsync);
        group.MapPost("/{code}", CreateVersionAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        var stored = await db.InstitutionalRuleSettings.AsNoTracking()
            .Where(x => x.Code.StartsWith("system.") && x.Status == "active")
            .OrderByDescending(x => x.EffectiveFrom).ToListAsync(cancellationToken);
        var items = Definitions.Select(pair =>
        {
            var active = stored.FirstOrDefault(x => x.Code == pair.Key);
            return new SystemSettingResponse(pair.Key, pair.Value.Category, pair.Value.Label, pair.Value.ValueType,
                active?.Value ?? pair.Value.DefaultValue, active?.EffectiveFrom ?? new DateOnly(2026, 1, 1),
                active?.SourceReference ?? "Configuración base Proyecto Centenario", active is null ? "default" : "active");
        });
        return Results.Ok(new { total = Definitions.Count, items });
    }

    private static async Task<IResult> CreateVersionAsync(string code, CreateSystemSettingVersionRequest request, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken cancellationToken)
    {
        if (!access.CanConfigureSystem(context.User)) return Results.Forbid();
        if (!Definitions.TryGetValue(code, out var definition)) return Results.BadRequest(new { message = "El parámetro no pertenece al catálogo administrable." });
        var value = request.Value.Trim();
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Valor y fundamento son obligatorios." });
        if (definition.ValueType == "integer" && (!int.TryParse(value, out var number) || number < 0 || number > 3650)) return Results.BadRequest(new { message = "El valor numérico configurado no es válido." });
        var duplicate = await db.InstitutionalRuleSettings.AnyAsync(x => x.Code == code && x.EffectiveFrom == request.EffectiveFrom, cancellationToken);
        if (duplicate) return Results.Conflict(new { message = "Ya existe una versión del parámetro con esa fecha de vigencia." });
        var previous = await db.InstitutionalRuleSettings.Where(x => x.Code == code && x.Status == "active" && (x.EffectiveTo == null || x.EffectiveTo >= request.EffectiveFrom)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(cancellationToken);
        if (previous is not null) { previous.EffectiveTo = request.EffectiveFrom.AddDays(-1); previous.Status = "retired"; }
        var entity = new InstitutionalRuleSetting { Code = code, Value = value, EffectiveFrom = request.EffectiveFrom, Status = "active", SourceReference = request.SourceReference.Trim() };
        db.InstitutionalRuleSettings.Add(entity);
        audit.Add(context, "system.setting.version_created", nameof(InstitutionalRuleSetting), entity.Id.ToString(), null, AuditResults.Success, new { entity.Code, entity.Value, entity.EffectiveFrom, entity.SourceReference, previousId = previous?.Id });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/system/settings/{Uri.EscapeDataString(code)}", new SystemSettingResponse(code, definition.Category, definition.Label, definition.ValueType, entity.Value, entity.EffectiveFrom, entity.SourceReference, entity.Status));
    }

    private sealed record Definition(string Category, string Label, string ValueType, string DefaultValue);
}

public sealed record CreateSystemSettingVersionRequest(string Value, DateOnly EffectiveFrom, string SourceReference);
public sealed record SystemSettingResponse(string Code, string Category, string Label, string ValueType, string Value, DateOnly EffectiveFrom, string SourceReference, string Status);
