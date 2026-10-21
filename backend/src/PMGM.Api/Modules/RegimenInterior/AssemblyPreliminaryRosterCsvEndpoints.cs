using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Descarga INTERNA para revisión de asambleístas; nunca padrón cerrado
/// ni lista acreditada de sufragantes. No acepta datos de participantes
/// ni decisiones financieras desde el navegador.
/// </summary>
public static class AssemblyPreliminaryRosterCsvEndpoints
{
    public static async Task<IResult> GetAsync(
        Guid assemblyId, HttpContext context,
        RegimenInteriorDbContext db, PmgmDbContext institutionalDb,
        IAuditService audit, CancellationToken ct)
    {
        if (!AssemblyPreliminaryReviewEndpoints.CanRead(context.User))
            return Results.Forbid();
        if (assemblyId == Guid.Empty)
            return Results.BadRequest(new { code = "INVALID_ASSEMBLY_ID" });

        var assembly = await db.Assemblies.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == assemblyId, ct);
        if (assembly is null)
            return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });
        if (assembly.Status != "draft" || assembly.ClosedAtUtc is not null ||
            assembly.ClosedBySubject is not null || assembly.ClosureEvidenceDigest is not null)
            return Results.Conflict(new { code = "ASSEMBLY_NOT_DRAFT" });

        // The endpoint is fail-closed until canonical historical eligibility
        // coverage is available: EVERY candidate is marked pending, no ballot
        // list is released, even if a stored preliminary decision says eligible.
        var members = await db.AssemblyMemberStatuses.AsNoTracking()
            .Where(m => m.AssemblyId == assemblyId)
            .OrderBy(m => m.Id).Take(10001).ToArrayAsync(ct);
        if (members.Length is 0 or > 10000 ||
            members.Any(m => m.PersonId == Guid.Empty ||
                m.MemberId == Guid.Empty || m.OrganizationId == Guid.Empty ||
                m.SourceOfficeAssignmentId == Guid.Empty ||
                string.IsNullOrWhiteSpace(m.SourceEvidenceReference) ||
                m.VerifiedAtUtc == default))
            return Results.Conflict(new { code = "PRELIMINARY_ASSEMBLY_SOURCE_INCOMPLETE" });

        var pending = new AssemblyEligibilityDecision(
            AssemblyVoterEligibilityPolicy.Pending,
            false, false, "OFFICIAL_SOURCES_PENDING");

        try
        {
            var candidates = members.Select(m => new AssemblyRosterCandidate(
                m.PersonId, m.LastNamesAtCutoff, m.FirstNamesAtCutoff,
                m.RutAtCutoff, m.WorkshopAtCutoff, pending));
            var csv = AssemblyRosterCsvPolicy.BuildPreview(candidates);
            if (csv.MayPublishAsOfficialRoster || csv.MayAuthorizeSuffrage)
                return Results.Conflict(new { code = "ROSTER_NOT_PRELIMINARY" });

            audit.Add(context, "regimen.asamblea.preliminary_members_csv.consulted",
                nameof(AssemblyRecord), assemblyId.ToString("D"), null,
                AuditResults.Success, new { AssemblyStatus = assembly.Status, Count = members.Length });
            await institutionalDb.SaveChangesAsync(ct);

            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers["X-PMGM-Official-Roster"] = "false";
            context.Response.Headers["X-PMGM-May-Authorize-Suffrage"] = "false";
            return Results.File(csv.GeneralAssemblyMembersCsvUtf8,
                "text/csv; charset=utf-8",
                "asambleistas-preliminar-" + assemblyId.ToString("N") + ".csv");
        }
        catch (ArgumentException)
        {
            return Results.Conflict(new { code = "INVALID_PRELIMINARY_ASSEMBLY_DATA" });
        }
    }
}
