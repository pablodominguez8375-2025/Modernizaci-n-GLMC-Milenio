using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.Ceremonies;

public static class GrandMasterCeremonyEndpoints
{
    public static IEndpointRouteBuilder MapGrandMasterCeremonyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ceremonias")
            .WithTags("Ceremonias - Gran Maestría")
            .RequireAuthorization();

        group.MapPost("/solicitudes/{requestId:guid}/validaciones/gran-maestria", SetGrandMasterValidationAsync);
        group.MapGet("/solicitudes/{requestId:guid}/validaciones/gran-maestria", GetGrandMasterValidationAsync);
        group.MapGet("/solicitudes/{requestId:guid}/matriz-habilitacion", GetAuthorizationMatrixAsync);
        return endpoints;
    }

    private static async Task<IResult> SetGrandMasterValidationAsync(
        Guid requestId,
        GrandMasterValidationRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanProvideGrandMasterApproval(httpContext.User)) return Results.Forbid();

        if (request.Status is not CeremonyCodes.ValidationStatus.Approved
            and not CeremonyCodes.ValidationStatus.Observed
            and not CeremonyCodes.ValidationStatus.Rejected)
            return Results.BadRequest(new { message = "Estado de visto bueno de Gran Maestría no válido." });

        var ceremony = await db.CeremonyRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null) return Results.NotFound();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia ya se encuentra autorizada." });

        var validation = new CeremonyValidation
        {
            CeremonyRequestId = ceremony.Id,
            ValidationType = CeremonyCodes.ValidationType.GrandMaster,
            Status = request.Status,
            AsOfDate = ChileToday(),
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        db.CeremonyValidations.Add(validation);
        audit.Add(
            httpContext,
            "ceremony.grand_master_validation.recorded",
            nameof(CeremonyValidation),
            validation.Id.ToString(),
            ceremony.OrganizationId,
            request.Status == CeremonyCodes.ValidationStatus.Approved ? AuditResults.Success : AuditResults.Rejected,
            new { validation.Status, validation.AsOfDate, validation.SourceReference });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            validation.Id,
            validation.CeremonyRequestId,
            validation.ValidationType,
            validation.Status,
            validation.AsOfDate,
            validation.SourceReference,
            validation.RecordedAtUtc
        });
    }

    private static async Task<IResult> GetGrandMasterValidationAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanEvaluateCeremonies(httpContext.User) && !access.CanReadOrganization(httpContext.User, ceremony.OrganizationId))
            return Results.Forbid();

        var validation = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId && x.ValidationType == CeremonyCodes.ValidationType.GrandMaster)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (validation is null)
            return Results.Ok(new
            {
                requestId,
                validationType = CeremonyCodes.ValidationType.GrandMaster,
                status = CeremonyCodes.ValidationStatus.Pending,
                canAuthorize = false
            });

        return Results.Ok(new
        {
            requestId,
            validation.Id,
            validation.ValidationType,
            validation.Status,
            validation.AsOfDate,
            validation.SourceReference,
            validation.Notes,
            validation.RecordedAtUtc,
            canAuthorize = GrandMasterCeremonyAuthorizationPolicy.CanAuthorize(validation.Status)
        });
    }

    private static async Task<IResult> GetAuthorizationMatrixAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null) return Results.NotFound();

        if (!access.CanEvaluateCeremonies(httpContext.User) && !access.CanReadOrganization(httpContext.User, ceremony.OrganizationId))
            return Results.Forbid();

        var today = ChileToday();
        var now = DateTimeOffset.UtcNow;

        var internalAffairs = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId && x.ValidationType == CeremonyCodes.ValidationType.InternalAffairs)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var grandMaster = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId && x.ValidationType == CeremonyCodes.ValidationType.GrandMaster)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var treasury = await db.FinancialRegularitySnapshots.AsNoTracking()
            .Where(x => x.OrganizationId == ceremony.OrganizationId &&
                        x.MemberId == null &&
                        x.Scope == TreasuryCodes.RegularityScope.Organization &&
                        x.AsOfDate <= today)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var hospitalaria = await db.HospitalariaRegularitySnapshots.AsNoTracking()
            .Where(x => x.OrganizationId == ceremony.OrganizationId && x.AsOfDate <= today)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        CandidatePublicationEvidence? publicationEvidence = null;
        Guid? publicationId = null;
        if (ceremony.CeremonyType == CeremonyCodes.Type.Initiation)
        {
            var publication = await db.CandidatePublications.AsNoTracking()
                .Where(x => x.CeremonyRequestId == requestId)
                .OrderByDescending(x => x.PublishedFromUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (publication is not null)
            {
                publicationId = publication.Id;
                var validUntil = publication.PublishedUntilUtc is null || publication.PublishedUntilUtc > now
                    ? now
                    : publication.PublishedUntilUtc.Value;
                var completedDays = validUntil < publication.PublishedFromUtc
                    ? 0
                    : Math.Max(0, (int)Math.Floor((validUntil - publication.PublishedFromUtc).TotalDays));

                publicationEvidence = new CandidatePublicationEvidence(
                    publication.Id,
                    publication.Status,
                    publication.RequiredDays,
                    completedDays,
                    publication.RuleCode);
            }
        }

        var decision = CeremonyEligibilityPolicy.Evaluate(
            ceremony.CeremonyType,
            internalAffairs?.Status,
            treasury?.Status,
            hospitalaria?.Status,
            grandMaster?.Status,
            publicationEvidence);

        var requirements = decision.Requirements.ToList();
        CeremonyValidation? finalBallot = null;
        if (ceremony.CeremonyType == CeremonyCodes.Type.Initiation)
        {
            finalBallot = await db.CeremonyValidations.AsNoTracking()
                .Where(x => x.CeremonyRequestId == requestId && x.ValidationType == CeremonyCodes.ValidationType.CandidateFinalBallot)
                .OrderByDescending(x => x.RecordedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var ballotStatus = finalBallot?.Status == CeremonyCodes.ValidationStatus.Approved
                ? CeremonyCodes.ValidationStatus.Approved
                : finalBallot?.Status == CeremonyCodes.ValidationStatus.Rejected
                    ? CeremonyCodes.ValidationStatus.Rejected
                    : CeremonyCodes.ValidationStatus.Observed;

            var ballotReason = ballotStatus == CeremonyCodes.ValidationStatus.Approved
                ? "El balotaje de primer grado se encuentra aprobado y registrado."
                : ballotStatus == CeremonyCodes.ValidationStatus.Rejected
                    ? "El último balotaje registrado fue rechazado."
                    : "El balotaje de primer grado aún no registra una aprobación habilitante.";

            requirements.Add(new CeremonyRequirementResult(
                "candidate_final_ballot",
                "Balotaje de primer grado",
                ballotStatus,
                ballotReason));
        }

        var overall = requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Rejected)
            ? "does_not_comply"
            : requirements.Any(x => x.Status == CeremonyCodes.ValidationStatus.Observed)
                ? "observed"
                : "complies";

        return Results.Ok(new
        {
            requestId,
            ceremony.CeremonyType,
            ceremony.OrganizationId,
            evaluatedAsOf = today,
            status = overall,
            canAuthorize = overall == "complies",
            requirements,
            evidence = new
            {
                regimenInteriorValidationId = internalAffairs?.Id,
                treasurySnapshotId = treasury?.Id,
                hospitalariaSnapshotId = hospitalaria?.Id,
                granMaestriaValidationId = grandMaster?.Id,
                publicationId,
                finalBallotValidationId = finalBallot?.Id
            }
        });
    }

    private static DateOnly ChileToday()
    {
        var chileNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chileNow.DateTime);
    }
}

public sealed record GrandMasterValidationRequest(string Status, string? SourceReference, string? Notes);

public static class GrandMasterCeremonyAuthorizationPolicy
{
    public static bool CanAuthorize(string? status)
        => string.Equals(status, CeremonyCodes.ValidationStatus.Approved, StringComparison.OrdinalIgnoreCase);
}
