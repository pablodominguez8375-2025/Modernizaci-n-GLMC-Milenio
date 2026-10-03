using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionExternalIntake
{
    public static async Task<IResult> CreateAsync(CreateExternalIncorporationRequest request, HttpContext httpContext,
        PmgmDbContext coreDb, AdmissionsDbContext admissionsDb, IInstitutionalAccessService access,
        IAuditService audit, CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "private, no-store";
        if (!access.CanManageOrganization(httpContext.User, request.OrganizationId) && !access.CanEvaluateCeremonies(httpContext.User))
            return Results.Forbid();
        var first = request.FirstNames?.Trim(); var last = request.LastNames?.Trim();
        var identifier = request.RutOrInstitutionalId?.Trim().Replace(".", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();
        var obedience = request.OriginObedience?.Trim(); var lodge = request.OriginLodgeName?.Trim();
        var number = request.OriginLodgeNumber?.Trim();
        if (string.IsNullOrWhiteSpace(first) || first.Length > 160 || string.IsNullOrWhiteSpace(last) || last.Length > 160 ||
            string.IsNullOrWhiteSpace(identifier) || identifier.Length > 16 || !identifier.All(char.IsLetterOrDigit) ||
            string.IsNullOrWhiteSpace(obedience) || obedience.Length > 240 || lodge?.Length > 240 || number?.Length > 80 ||
            request.Degree is not "apprentice" and not "fellowcraft" and not "master")
            return Results.BadRequest(new { message = "Revise nombres, apellidos, identificación (hasta 16 caracteres), Obediencia y grado declarado." });
        if (!await coreDb.Organizations.AsNoTracking().AnyAsync(x => x.Id == request.OrganizationId && x.Type == "workshop", cancellationToken))
            return Results.NotFound(new { message = "El Taller destino no existe." });

        // Both contexts use the same database. Persona, expediente and audit must commit together.
        admissionsDb.Database.SetDbConnection(coreDb.Database.GetDbConnection());
        await using var transaction = await coreDb.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await admissionsDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
        try
        {
            if (await coreDb.People.AsNoTracking().AnyAsync(x => x.Rut != null &&
                    x.Rut.Replace(".", "").Replace("-", "").Replace(" ", "").ToUpper() == identifier, cancellationToken))
                return Conflict();
            var person = new Person { FirstNames = first, LastNames = last, Rut = identifier };
            var entity = new AdmissionCase
            {
                OrganizationId = request.OrganizationId, PersonId = person.Id, AdmissionType = "incorporation",
                OriginObedience = obedience, OriginLodgeName = string.IsNullOrWhiteSpace(lodge) ? null : lodge,
                OriginLodgeNumber = string.IsNullOrWhiteSpace(number) ? null : number, Degree = request.Degree,
                WageIncreaseEvidenceApplies = request.Degree is "fellowcraft" or "master",
                ExaltationEvidenceApplies = request.Degree == "master", HasPeaceAndFriendshipPact = null,
                Status = AdmissionWorkflowCodes.CaseStatus.UnderReview,
                CreatedBySubject = httpContext.User.FindFirst("sub")?.Value ?? httpContext.User.Identity?.Name ?? "unknown"
            };
            coreDb.People.Add(person);
            audit.Add(httpContext, "admission.external.intake.created", nameof(AdmissionCase), entity.Id.ToString(),
                entity.OrganizationId, AuditResults.Success, new { entity.PersonId, entity.AdmissionType, entity.Status });
            await coreDb.SaveChangesAsync(cancellationToken);
            admissionsDb.AdmissionCases.Add(entity);
            await admissionsDb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created($"/api/admisiones/expedientes/{entity.Id}", new
            {
                entity.Id, entity.OrganizationId, entity.AdmissionType, entity.PersonId, entity.MemberId,
                entity.AffiliationMode, entity.WithdrawalLetterGrantedDate, entity.Status, entity.CreatedAtUtc
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState is "23505" or "40001")
        { return Conflict(); }
        catch (PostgresException ex) when (ex.SqlState == "40001") { return Conflict(); }
    }
    private static IResult Conflict() => Results.Conflict(new { message = "No fue posible registrar esta identidad. Revise el expediente existente con el área autorizada antes de repetir el alta." });
}

public sealed record CreateExternalIncorporationRequest(Guid OrganizationId, string FirstNames, string LastNames,
    string RutOrInstitutionalId, string OriginObedience, string Degree, string? OriginLodgeName = null, string? OriginLodgeNumber = null);
