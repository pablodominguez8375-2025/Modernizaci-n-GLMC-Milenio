using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Informe «Qué falta para cerrar el padrón». Solo lectura y diagnóstico: no certifica fuentes, no
/// cierra la Asamblea ni concede sufragio. Resume, por fuente institucional, qué está acreditado y qué falta.
/// </summary>
public static class AssemblyClosureReadinessEndpoints
{
    /// <summary>Códigos con que el registro canónico puede nombrar el cargo de Venerable Maestro.</summary>
    public static readonly string[] MasterOfficeCodes = ["venerable_master", "worshipful_master"];

    public static async Task<IResult> GetAsync(
        Guid assemblyId, HttpContext context, RegimenInteriorDbContext db, PmgmDbContext appDb,
        IAuditService audit, CancellationToken ct)
    {
        if (!AssemblyPreliminaryReviewEndpoints.CanRead(context.User)) return Results.Forbid();
        if (assemblyId == Guid.Empty) return Results.BadRequest(new { code = "INVALID_ASSEMBLY_ID" });
        context.Response.Headers.CacheControl = "private, no-store";

        var assembly = await db.Assemblies.AsNoTracking().SingleOrDefaultAsync(a => a.Id == assemblyId, ct);
        if (assembly is null) return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });

        var members = await db.AssemblyMemberStatuses.AsNoTracking().Where(x => x.AssemblyId == assemblyId)
            .OrderBy(x => x.Id).Take(10001).ToArrayAsync(ct);
        if (members.Length > 10000) return Results.Conflict(new { code = "ASSEMBLY_TOO_LARGE" });
        var history = await db.AssemblyEligibilities.AsNoTracking().Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);
        var byMember = history.GroupBy(h => h.MemberStatusId).ToDictionary(g => g.Key, g => (IReadOnlyCollection<AssemblyEligibilityRecord>)g.ToArray());
        var statuses = members.Select(m => AssemblyPreliminaryReviewProjection.Project(m, byMember.GetValueOrDefault(m.Id) ?? [], assembly.CutoffDate).Status).ToArray();

        var workshopIds = members.Select(m => m.OrganizationId).Distinct().ToArray();
        var signedWorkshops = workshopIds.Length == 0 ? [] : await db.AssemblyTreasurySignedReviews.AsNoTracking()
            .Where(x => x.AssemblyId == assemblyId && workshopIds.Contains(x.OrganizationId))
            .Select(x => x.OrganizationId).Distinct().ToArrayAsync(ct);
        var attested = AssemblyAttestationPolicy.Current(
            await db.AssemblyAttestationEvents.AsNoTracking().Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct))
            .Keys.ToArray();
        var restrictions = await db.InstitutionalRestrictions.AsNoTracking().CountAsync(x => x.RevokedAtUtc == null, ct);

        AssemblyMemberSourceInventoryResult? inventory = null; string? officeCode = null;
        foreach (var code in MasterOfficeCodes)
        {
            var attempt = await AssemblyMemberSourceInventory.DiscoverAsync(appDb, assembly.CutoffDate, code, ct);
            if (attempt.BlockingReasons.Contains("MASTER_OFFICE_RECORDS_MISSING")) { inventory ??= attempt; continue; }
            inventory = attempt; officeCode = code; break;
        }

        var report = AssemblyClosureReadinessBuilder.Build(assembly, new AssemblyReadinessFacts(
            inventory?.CandidateFacts.Count ?? 0, inventory?.BlockingReasons ?? ["MASTER_OFFICE_RECORDS_MISSING"], officeCode,
            members.Length, statuses, workshopIds.Length, signedWorkshops.Length, restrictions, attested));

        audit.Add(context, "regimen.asamblea.cierre.estado.consultado", nameof(AssemblyRecord), assemblyId.ToString("D"),
            null, AuditResults.Success, new { Items = report.Items.Count, MayClose = report.MayClose });
        await appDb.SaveChangesAsync(ct);
        return Results.Ok(report);
    }
}

public sealed record AssemblyReadinessFacts(
    int MasterCandidatesFound, IReadOnlyList<string> InventoryBlockingReasons, string? MasterOfficeCode,
    int MaterializedMembers, IReadOnlyList<string> MemberStatuses,
    int Workshops, int WorkshopsWithSignedTreasuryReview, int ActiveRestrictions,
    IReadOnlyCollection<string>? AttestedKinds = null);

public sealed record AssemblyReadinessItem(string Code, string Title, string State, string Detail, int? Count);

public sealed record AssemblyClosureReadiness(
    Guid AssemblyId, string Name, string Status, DateOnly MeetingDate, DateOnly CutoffDate,
    bool MayClose, bool Provisional, IReadOnlyList<AssemblyReadinessItem> Items, IReadOnlyList<string> BlockingCodes);

/// <summary>Proyección pura (testeable sin base de datos). Estados: ok, pending, blocked.</summary>
public static class AssemblyClosureReadinessBuilder
{
    public const string Ok = "ok", Pending = "pending", Blocked = "blocked";

    public static AssemblyClosureReadiness Build(AssemblyRecord assembly, AssemblyReadinessFacts f)
    {
        var items = new List<AssemblyReadinessItem>();
        var isDraft = assembly.Status == "draft" && assembly.ClosedAtUtc is null;
        items.Add(new("assembly", "Asamblea y fecha de corte",
            isDraft && assembly.CutoffDate <= assembly.MeetingDate ? Ok : Blocked,
            isDraft ? $"Corte al {assembly.CutoffDate:dd-MM-yyyy}, reunión el {assembly.MeetingDate:dd-MM-yyyy}."
                    : "La Asamblea ya no está en borrador.", null));

        var attested = f.AttestedKinds ?? [];
        var masterAttested = attested.Contains(AssemblyAttestationPolicy.MasterPeriods);
        var offices = f.MasterCandidatesFound > 0
            ? new AssemblyReadinessItem("master_terms", "Períodos de Venerable Maestro", masterAttested ? Ok : Pending,
                masterAttested
                    ? $"Régimen Interior certificó los períodos de los {f.MasterCandidatesFound} Venerables Maestros con su acta."
                    : $"Se encontraron {f.MasterCandidatesFound} hermanos con cargo de Venerable Maestro. Falta la constancia oficial de cada período (acta).", f.MasterCandidatesFound)
            : new AssemblyReadinessItem("master_terms", "Períodos de Venerable Maestro", Blocked,
                f.InventoryBlockingReasons.Contains("MASTER_OFFICE_RECORDS_MISSING")
                    ? "No hay cargos de Venerable Maestro registrados en las fichas. Primero deben cargarse (Carga inicial)."
                    : "Los registros de Venerables Maestros tienen inconsistencias: " + string.Join(", ", f.InventoryBlockingReasons) + ".", 0);
        items.Add(offices);

        var membershipAttested = attested.Contains(AssemblyAttestationPolicy.Membership);
        items.Add(new("membership", "Calidad de miembro activo", membershipAttested ? Ok : Pending,
            membershipAttested ? "Régimen Interior certificó la calidad de miembro activo de los asambleístas al corte, con su acta."
                               : "Falta la constancia de Régimen Interior sobre la calidad de miembro activo al corte (acta).", null));

        items.Add(new("roster", "Asambleístas acreditados", f.MaterializedMembers > 0 ? Ok : Pending,
            f.MaterializedMembers > 0 ? $"{f.MaterializedMembers} asambleístas acreditados para esta Asamblea."
                                      : "Todavía no se han acreditado asambleístas.", f.MaterializedMembers));

        var evaluated = f.MemberStatuses.Count(s => s is AssemblyVoterEligibilityPolicy.Eligible or AssemblyVoterEligibilityPolicy.Ineligible or AssemblyVoterEligibilityPolicy.AttendanceOnly);
        var pending = f.MaterializedMembers - evaluated;
        items.Add(new("evaluation", "Evaluación de habilitación", f.MaterializedMembers == 0 ? Pending : pending == 0 ? Ok : Pending,
            f.MaterializedMembers == 0 ? "No hay asambleístas para evaluar."
                : pending == 0 ? $"Los {evaluated} asambleístas tienen una evaluación vigente."
                : $"{pending} de {f.MaterializedMembers} asambleístas siguen pendientes de evaluación.", pending));

        var gtOk = f.Workshops > 0 && f.WorkshopsWithSignedTreasuryReview == f.Workshops;
        items.Add(new("grand_treasury", "Saldo certificado de Gran Tesorería", f.Workshops == 0 ? Pending : gtOk ? Ok : Pending,
            f.Workshops == 0 ? "Se evalúa cuando haya asambleístas acreditados."
                : gtOk ? $"Los {f.Workshops} Talleres tienen revisión firmada de Gran Tesorería."
                : $"{f.WorkshopsWithSignedTreasuryReview} de {f.Workshops} Talleres tienen revisión firmada de Gran Tesorería.", f.Workshops - f.WorkshopsWithSignedTreasuryReview));

        var honor = attested.Contains(AssemblyAttestationPolicy.HonorCoverage);
        var regimen = attested.Contains(AssemblyAttestationPolicy.RegimenCoverage);
        var restrictionNote = f.ActiveRestrictions > 0 ? $" ({f.ActiveRestrictions} restricciones vigentes registradas)." : " (sin restricciones registradas).";
        items.Add(new("rights", "Tribunal de Honor y Régimen Interior", honor && regimen ? Ok : Pending,
            honor && regimen ? "Ambas fuentes están certificadas como completas al corte" + restrictionNote
            : honor ? "Falta la constancia de cobertura completa de Régimen Interior al corte" + restrictionNote
            : regimen ? "Falta la constancia de cobertura completa del Tribunal de Honor al corte" + restrictionNote
            : "Falta la declaración de cobertura completa de ambas fuentes al corte" + restrictionNote, f.ActiveRestrictions));

        var blocking = items.Where(i => i.State != Ok).Select(i => i.Code).ToList();
        items.Add(new("closure", "Cierre del padrón", Blocked,
            "El cierre se habilita cuando todos los pasos anteriores estén completos y certificados.", null));
        blocking.Add("closure");
        return new AssemblyClosureReadiness(assembly.Id, assembly.Name, assembly.Status, assembly.MeetingDate, assembly.CutoffDate,
            false, true, items, blocking);
    }
}
