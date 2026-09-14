using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionCeremonyEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionCeremonyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admisiones")
            .WithTags("Afiliaciones e incorporaciones - solicitud de ceremonia")
            .RequireAuthorization();

        group.MapPost("/expedientes/{caseId:guid}/solicitud-ceremonia", CreateCeremonyRequestAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateCeremonyRequestAsync(
        Guid caseId,
        CreateAdmissionCeremonyRequest request,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (admissionCase is null) return Results.NotFound();

        if (!access.CanManageOrganization(httpContext.User, admissionCase.OrganizationId))
            return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya se encuentra resuelto." });
        if (request.Notes?.Length > 2000)
            return Results.BadRequest(new { message = "Las observaciones no pueden exceder 2000 caracteres." });

        var existing = await coreDb.CeremonyRequests
            .SingleOrDefaultAsync(x => x.AdmissionCaseId == caseId, cancellationToken);
        if (existing is not null)
        {
            await ReconcileCaseLinkAsync(admissionCase, existing.Id, admissionsDb, cancellationToken);
            return Results.Ok(new
            {
                existing.Id,
                existing.AdmissionCaseId,
                existing.OrganizationId,
                existing.CeremonyType,
                existing.MemberId,
                existing.ProposedDate,
                existing.Status,
                alreadyCreated = true
            });
        }

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);
        if (!projection.Decision.CanProceed)
        {
            admissionCase.Status = projection.Decision.Status == "does_not_comply"
                ? AdmissionWorkflowCodes.CaseStatus.Rejected
                : AdmissionWorkflowCodes.CaseStatus.Observed;
            await admissionsDb.SaveChangesAsync(cancellationToken);

            return Results.Conflict(new
            {
                message = "El expediente todavía no cumple todos los requisitos para solicitar la ceremonia.",
                eligibility = projection.Decision
            });
        }

        var ceremony = new CeremonyRequest
        {
            OrganizationId = admissionCase.OrganizationId,
            CeremonyType = admissionCase.AdmissionType,
            MemberId = admissionCase.AdmissionType == CeremonyCodes.Type.Affiliation
                ? admissionCase.MemberId
                : null,
            CandidatePersonId = null,
            AdmissionCaseId = admissionCase.Id,
            ProposedDate = request.ProposedDate,
            Status = CeremonyCodes.RequestStatus.UnderReview,
            Notes = Normalize(request.Notes)
        };

        coreDb.CeremonyRequests.Add(ceremony);
        coreDb.CeremonyValidations.Add(new CeremonyValidation
        {
            CeremonyRequestId = ceremony.Id,
            ValidationType = CeremonyCodes.ValidationType.AdmissionProcedure,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = ChileToday(),
            SourceReference = admissionCase.Id.ToString(),
            Notes = "El expediente de admisión cumplía los requisitos procedimentales al crear la solicitud de ceremonia."
        });

        audit.Add(
            httpContext,
            "admission.ceremony_request.created",
            nameof(CeremonyRequest),
            ceremony.Id.ToString(),
            ceremony.OrganizationId,
            AuditResults.Success,
            new
            {
                ceremony.CeremonyType,
                ceremony.AdmissionCaseId,
                ceremony.ProposedDate,
                ceremony.Status
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        await ReconcileCaseLinkAsync(admissionCase, ceremony.Id, admissionsDb, cancellationToken);

        return Results.Created($"/api/ceremonias/solicitudes/{ceremony.Id}", new
        {
            ceremony.Id,
            ceremony.AdmissionCaseId,
            ceremony.OrganizationId,
            ceremony.CeremonyType,
            ceremony.MemberId,
            ceremony.ProposedDate,
            ceremony.Status,
            alreadyCreated = false,
            eligibility = projection.Decision
        });
    }

    private static async Task ReconcileCaseLinkAsync(
        AdmissionCase admissionCase,
        Guid ceremonyRequestId,
        AdmissionsDbContext admissionsDb,
        CancellationToken cancellationToken)
    {
        admissionCase.Status = AdmissionWorkflowCodes.CaseStatus.Eligible;

        var reference = ceremonyRequestId.ToString();
        var alreadyRecorded = admissionCase.Decisions.Any(x =>
            x.DecisionType == AdmissionWorkflowCodes.DecisionType.CeremonyRequestCreated &&
            x.SourceReference == reference);

        if (!alreadyRecorded)
        {
            admissionsDb.AdmissionDecisions.Add(new AdmissionDecision
            {
                AdmissionCaseId = admissionCase.Id,
                DecisionType = AdmissionWorkflowCodes.DecisionType.CeremonyRequestCreated,
                Status = CeremonyCodes.ValidationStatus.Approved,
                AsOfDate = ChileToday(),
                SourceReference = reference,
                Notes = "Solicitud de ceremonia creada desde expediente habilitado.",
                RecordedBySubject = admissionCase.CreatedBySubject
            });
        }

        await admissionsDb.SaveChangesAsync(cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly ChileToday()
    {
        var chile = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chile.DateTime);
    }
}

public sealed record CreateAdmissionCeremonyRequest(DateOnly? ProposedDate, string? Notes);
