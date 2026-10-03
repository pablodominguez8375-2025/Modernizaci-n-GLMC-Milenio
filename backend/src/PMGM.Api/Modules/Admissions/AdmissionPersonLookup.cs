using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionPersonLookup
{
    public static async Task<IResult> SearchAsync(
        Guid organizationId, string admissionType, string query,
        HttpContext httpContext, PmgmDbContext coreDb, AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "private, no-store";
        var central = access.CanEvaluateCeremonies(httpContext.User);
        if (!access.CanManageOrganization(httpContext.User, organizationId) && !central)
            return Results.Forbid();
        if (admissionType is not CeremonyCodes.Type.Affiliation and not CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "Tipo de admisión no válido." });
        var term = query.Trim();
        if (term.Length is < 3 or > 80)
            return Results.BadRequest(new { message = "Ingrese entre 3 y 80 caracteres para buscar." });
        if (!await coreDb.Organizations.AnyAsync(x => x.Id == organizationId && x.Type != "order", cancellationToken))
            return Results.NotFound();
        var normalized = term.ToUpperInvariant();
        List<AdmissionPersonOption> items;
        if (admissionType == CeremonyCodes.Type.Affiliation)
        {
            items = await coreDb.Members.AsNoTracking()
                .Where(x => (x.InstitutionalNumber != null && x.InstitutionalNumber.ToUpper() == normalized) ||
                    ((central || coreDb.Memberships.Any(m => m.MemberId == x.Id && m.OrganizationId == organizationId)) &&
                     (x.Person.FirstNames + " " + x.Person.LastNames).ToUpper().Contains(normalized)))
                .OrderBy(x => x.Person.LastNames).ThenBy(x => x.Person.FirstNames).ThenBy(x => x.Id)
                .Take(20)
                .Select(x => new AdmissionPersonOption(x.PersonId, x.Id,
                    x.Person.FirstNames + " " + x.Person.LastNames, x.InstitutionalNumber))
                .ToListAsync(cancellationToken);
        }
        else
        {
            // Never search unrestricted People: that would expose private insinuados.
            var personIds = await admissionsDb.AdmissionCases.AsNoTracking()
                .Where(x => x.AdmissionType == CeremonyCodes.Type.Incorporation &&
                    (central || x.OrganizationId == organizationId))
                .Select(x => x.PersonId).Distinct().ToListAsync(cancellationToken);
            items = await coreDb.People.AsNoTracking()
                .Where(x => personIds.Contains(x.Id) && !coreDb.Members.Any(m => m.PersonId == x.Id) &&
                    (x.FirstNames + " " + x.LastNames).ToUpper().Contains(normalized))
                .OrderBy(x => x.LastNames).ThenBy(x => x.FirstNames).ThenBy(x => x.Id)
                .Take(20)
                .Select(x => new AdmissionPersonOption(x.Id, null, x.FirstNames + " " + x.LastNames, null))
                .ToListAsync(cancellationToken);
        }
        audit.Add(httpContext, "admission.identity.lookup", "AdmissionPersonLookup", organizationId.ToString(), organizationId,
            AuditResults.Success, new { admissionType, returned = items.Count });
        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { returned = items.Count, items });
    }
}

public sealed record AdmissionPersonOption(Guid PersonId, Guid? MemberId, string DisplayName, string? InstitutionalNumber);
