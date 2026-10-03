using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionIdentityGuard
{
    public static async Task<IResult?> ValidateAsync(CreateAdmissionCaseRequest request, bool central,
        PmgmDbContext coreDb, AdmissionsDbContext admissionsDb, CancellationToken cancellationToken)
    {
        if (!await coreDb.Organizations.AsNoTracking().AnyAsync(
                x => x.Id == request.OrganizationId && x.Type == "workshop", cancellationToken))
            return Results.NotFound(new { message = "El Taller destino no existe." });

        if (request.AdmissionType == CeremonyCodes.Type.Incorporation)
        {
            if (request.MemberId is not null)
                return Results.BadRequest(new { message = "Una incorporación no debe indicar membresía GLMCh antes de su resolución." });
            // An arbitrary PersonId must not grant access to private insinuados or other lodges.
            // New external intake will establish its own authorized identity context separately.
            if (!central && !await admissionsDb.AdmissionCases.AsNoTracking().AnyAsync(
                    x => x.PersonId == request.PersonId && x.OrganizationId == request.OrganizationId &&
                         x.AdmissionType == CeremonyCodes.Type.Incorporation, cancellationToken))
                return Results.Forbid();
        }

        if (!await coreDb.People.AsNoTracking().AnyAsync(x => x.Id == request.PersonId, cancellationToken))
            return Results.NotFound(new { message = "La persona no existe en la base maestra." });

        if (request.AdmissionType == CeremonyCodes.Type.Incorporation)
        {
            if (await coreDb.Members.AsNoTracking().AnyAsync(x => x.PersonId == request.PersonId, cancellationToken))
                return Results.BadRequest(new { message = "La persona ya tiene registro de hermano GLMCh; corresponde revisar su afiliación." });
            if (request.WithdrawalLetterGrantedDate is not null)
                return Results.BadRequest(new { message = "La fecha de Carta de Retiro Voluntario sólo aplica a afiliaciones." });
        }
        return null;
    }
}
