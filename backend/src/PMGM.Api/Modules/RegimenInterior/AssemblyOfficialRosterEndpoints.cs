using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Descarga OFICIAL (CSV o PDF) de los dos listados, SOLO de una Asamblea cerrada por el servicio de cierre
/// (estado closed + fecha + resumen SHA-256). Régimen Interior y Gran Secretaría. El documento oficial lleva el RUT COMPLETO (identificación correcta
/// del votante; validado por el PO/Jurídico, el control físico se regula fuera del software); las pantallas siguen enmascaradas;
/// la columna «Firma» siempre en blanco; cada descarga queda auditada. Una Asamblea en borrador responde 409.
/// </summary>
public static class AssemblyOfficialRosterEndpoints
{
    public static async Task<IResult> GetAsync(
        Guid assemblyId, string? listado, string? formato, HttpContext context,
        RegimenInteriorDbContext db, PmgmDbContext appDb, IAuditService audit, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!AssemblyPreliminaryReviewEndpoints.CanRead(context.User)) return Results.Forbid();
        var list = (listado ?? "sufragantes").Trim().ToLowerInvariant();
        var format = (formato ?? "pdf").Trim().ToLowerInvariant();
        if (assemblyId == Guid.Empty || list is not ("asambleistas" or "sufragantes") || format is not ("pdf" or "csv"))
            return Results.BadRequest(new { code = "INVALID_REQUEST" });

        var assembly = await db.Assemblies.AsNoTracking().SingleOrDefaultAsync(a => a.Id == assemblyId, ct);
        if (assembly is null) return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });
        if (assembly.Status != "closed" || assembly.ClosedAtUtc is null || string.IsNullOrWhiteSpace(assembly.ClosureEvidenceDigest))
            return Results.Conflict(new { code = "ASSEMBLY_NOT_CLOSED" });

        var members = await db.AssemblyMemberStatuses.AsNoTracking().Where(x => x.AssemblyId == assemblyId)
            .OrderBy(x => x.Id).Take(10001).ToArrayAsync(ct);
        if (members.Length is 0 or > 10000) return Results.Conflict(new { code = "ASSEMBLY_ROSTER_SIZE_INVALID" });
        var history = await db.AssemblyEligibilities.AsNoTracking().Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);
        var debtEvents = await db.AssemblyDebtDecisionEvents.AsNoTracking().Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);
        var roster = AssemblyRosterListingBuilder.Build(assembly, members, history, debtEvents);
        // Identificación completa para el documento oficial: el RUT viene del corte congelado de cada asambleísta.
        var fullRut = members.ToDictionary(m => m.PersonId, m => (m.RutAtCutoff ?? "").Trim());
        string Rut(Guid personId) => fullRut.GetValueOrDefault(personId, "");

        var closedOn = assembly.ClosedAtUtc.Value.UtcDateTime.ToString("yyyy-MM-dd");
        var asVoters = list == "sufragantes";
        // Asambleístas = quienes PUEDEN ASISTIR por su calidad (Ex Venerable Maestro o Venerable Maestro en su primer
        // período); NO es el padrón de sufragio. Se numera de nuevo y se indica la calidad, sin estado de sufragio.
        var attendees = roster.Asambleistas.Where(r => r.MayAttend).Select((r, i) => (Row: r, Number: i + 1)).ToArray();
        static string Quality(string category) => category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember
            ? "Asambleísta" : category == AssemblyMemberCategoryPolicy.Representative ? "Representante" : category;
        var count = asVoters ? roster.Sufragantes.Count : attendees.Length;
        var digest = assembly.ClosureEvidenceDigest!;
        var name = (asVoters ? "padron-sufragantes-" : "padron-asambleistas-") + closedOn + "-" + assemblyId.ToString("N")[..8];

        byte[] file; string type;
        if (format == "csv")
        {
            var rows = asVoters
                ? roster.Sufragantes.Select(v => new AssemblyRosterPrintRow(v.Number, v.Name, Rut(v.PersonId), v.Workshop, "", null)).ToArray()
                : attendees.Select(a => new AssemblyRosterPrintRow(a.Number, a.Row.Name, Rut(a.Row.PersonId), a.Row.Workshop, "", Quality(a.Row.Category))).ToArray();
            file = AssemblyRosterCsvPolicy.SerializeOfficial(rows, !asVoters, "Calidad"); type = "text/csv; charset=utf-8"; name += ".csv";
        }
        else
        {
            var cols = new List<string> { "N°", "Apellidos y nombres", "RUT", "Taller" }; var widths = new List<double> { 34, asVoters ? 250 : 230, 90, asVoters ? 200 : 180 };
            if (!asVoters) { cols.Add("Calidad"); widths.Add(130); }
            cols.Add("Firma"); widths.Add(asVoters ? 187 : 97);
            IReadOnlyList<IReadOnlyList<string>> body = asVoters
                ? roster.Sufragantes.Select(v => (IReadOnlyList<string>)[v.Number.ToString(), v.Name, Rut(v.PersonId), v.Workshop, ""]).ToArray()
                : attendees.Select(a => (IReadOnlyList<string>)[a.Number.ToString(), a.Row.Name, Rut(a.Row.PersonId), a.Row.Workshop, Quality(a.Row.Category), ""]).ToArray();
            file = AssemblyOfficialRosterPdf.Render(
                (asVoters ? "Padrón de sufragio — sufragantes habilitados" : "Listado de asambleístas — con derecho a asistir") + " — oficial",
                [$"Gran Logia Mixta de Chile · {assembly.Name}",
                 $"Asamblea: {assembly.MeetingDate:dd-MM-yyyy} · Corte del padrón: {assembly.CutoffDate:dd-MM-yyyy} · Cerrado el {closedOn} · {count} registros",
                 asVoters ? "Padrón de sufragio: quienes pueden votar. Orden alfabético por apellidos. El voto es siempre secreto."
                          : "Asisten por su calidad de Ex Venerable Maestro o de Venerable Maestro en su primer período. No es el padrón de sufragio. Orden alfabético por apellidos.",
                 "Documento reservado: contiene el RUT completo; su custodia y acceso se regulan institucionalmente."],
                cols, widths, body, $"Padron oficial cerrado el {closedOn} - Resumen SHA-256 {digest[..16]}");
            type = "application/pdf"; name += ".pdf";
        }

        audit.Add(context, "regimen.asamblea.padron_oficial.descargado", nameof(AssemblyRecord), assemblyId.ToString("D"), null,
            AuditResults.Success, new { Listado = list, Formato = format, Registros = count, RutCompleto = true, ClosureDigest = digest });
        await appDb.SaveChangesAsync(ct);
        context.Response.Headers["X-PMGM-Official-Roster"] = "true";
        context.Response.Headers["X-PMGM-Closure-Digest"] = digest;
        return Results.File(file, type, name);
    }
}
