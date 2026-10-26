using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Descarga OFICIAL (CSV o PDF) de los dos listados, SOLO de una Asamblea cerrada por el servicio de cierre
/// (estado closed + fecha + resumen SHA-256). Régimen Interior y Gran Secretaría; RUT siempre enmascarado;
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

        var closedOn = assembly.ClosedAtUtc.Value.UtcDateTime.ToString("yyyy-MM-dd");
        var asVoters = list == "sufragantes";
        var count = asVoters ? roster.Sufragantes.Count : roster.Asambleistas.Count;
        var digest = assembly.ClosureEvidenceDigest!;
        var name = (asVoters ? "padron-sufragantes-" : "padron-asambleistas-") + closedOn + "-" + assemblyId.ToString("N")[..8];

        byte[] file; string type;
        if (format == "csv")
        {
            var rows = asVoters
                ? roster.Sufragantes.Select(v => new AssemblyRosterPrintRow(v.Number, v.Name, v.Rut, v.Workshop, "", null)).ToArray()
                : roster.Asambleistas.Select(r => new AssemblyRosterPrintRow(r.Number, r.Name, r.Rut, r.Workshop, "", r.MayVote ? "Habilitado" : "Sin sufragio")).ToArray();
            file = AssemblyRosterCsvPolicy.SerializeOfficial(rows, !asVoters); type = "text/csv; charset=utf-8"; name += ".csv";
        }
        else
        {
            var cols = new List<string> { "N°", "Apellidos y nombres", "RUT", "Taller" }; var widths = new List<double> { 34, 250, 90, 200 };
            if (!asVoters) { cols.Add("Sufragio"); widths.Add(70); }
            cols.Add("Firma"); widths.Add(asVoters ? 187 : 117);
            IReadOnlyList<IReadOnlyList<string>> body = asVoters
                ? roster.Sufragantes.Select(v => (IReadOnlyList<string>)[v.Number.ToString(), v.Name, v.Rut, v.Workshop, ""]).ToArray()
                : roster.Asambleistas.Select(r => (IReadOnlyList<string>)[r.Number.ToString(), r.Name, r.Rut, r.Workshop, r.MayVote ? "Habilitado" : "Sin sufragio", ""]).ToArray();
            file = AssemblyOfficialRosterPdf.Render(
                (asVoters ? "Padrón de sufragantes habilitados" : "Asambleístas de la Gran Asamblea") + " — padrón oficial",
                [$"Gran Logia Mixta de Chile · {assembly.Name}",
                 $"Asamblea: {assembly.MeetingDate:dd-MM-yyyy} · Corte del padrón: {assembly.CutoffDate:dd-MM-yyyy} · Cerrado el {closedOn} · {count} registros",
                 "Orden alfabético por apellidos. El RUT se muestra parcialmente protegido. El voto es siempre secreto."],
                cols, widths, body, $"Padron oficial cerrado el {closedOn} - Resumen SHA-256 {digest[..16]}");
            type = "application/pdf"; name += ".pdf";
        }

        audit.Add(context, "regimen.asamblea.padron_oficial.descargado", nameof(AssemblyRecord), assemblyId.ToString("D"), null,
            AuditResults.Success, new { Listado = list, Formato = format, Registros = count, ClosureDigest = digest });
        await appDb.SaveChangesAsync(ct);
        context.Response.Headers["X-PMGM-Official-Roster"] = "true";
        context.Response.Headers["X-PMGM-Closure-Digest"] = digest;
        return Results.File(file, type, name);
    }
}
