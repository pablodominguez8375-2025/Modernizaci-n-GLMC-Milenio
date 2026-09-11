using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidateWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapCandidateWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/insinuados")
            .WithTags("Flujo de insinuaciones")
            .RequireAuthorization();

        group.MapPost("/solicitudes/{requestId:guid}/deliberacion-inicial", RecordInitialDeliberationAsync);
        group.MapPost("/solicitudes/{requestId:guid}/revision-tercer-grado", RecordThirdDegreeReviewAsync);
        group.MapPost("/solicitudes/{requestId:guid}/balotaje", RecordFinalBallotAsync);
        group.MapGet("/solicitudes/{requestId:guid}/flujo", GetWorkflowAsync);

        return endpoints;
    }

    private static async Task<IResult> RecordInitialDeliberationAsync(
        Guid requestId,
        InitialDeliberationRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremony = await GetInitiationAsync(requestId, coreDb, cancellationToken);
        if (ceremony is null) return Results.NotFound(new { message = "La solicitud de iniciación no existe." });
        if (!access.CanManageOrganization(httpContext.User, ceremony.OrganizationId)) return Results.Forbid();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia ya está autorizada y el expediente no admite nuevas deliberaciones." });

        var profile = await intakeDb.CandidateIntakeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, cancellationToken);
        if (profile is null) return Results.Conflict(new { message = "Debe existir una ficha privada de insinuación antes de registrar la deliberación." });
        if (request.DeliberationDate > ChileToday())
            return Results.BadRequest(new { message = "La deliberación no puede registrarse con fecha futura." });

        var decision = CandidateIntakeWorkflowPolicy.EvaluateInitialDeliberation(
            profile.InsinuationDate,
            request.DeliberationDate,
            request.PresentVoters,
            request.VotesInFavor,
            request.MinimumWaitingDays ?? 7);

        var status = ToValidationStatus(decision);
        var validation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateInitialDeliberation,
            status, request.DeliberationDate, request.SourceReference, decision.Reason);

        ceremony.Status = decision.IsRejected
            ? CeremonyCodes.RequestStatus.Rejected
            : CeremonyCodes.RequestStatus.UnderReview;

        audit.Add(httpContext, "candidate.workflow.initial_deliberation.recorded", nameof(CeremonyValidation),
            validation.Id.ToString(), ceremony.OrganizationId,
            decision.IsRejected ? AuditResults.Rejected : AuditResults.Success,
            new
            {
                profile.InsinuationDate,
                request.DeliberationDate,
                request.PresentVoters,
                request.VotesInFavor,
                decision.Code,
                status
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { validation.Id, validation.Status, decision.Code, decision.Reason, ceremony.Status });
    }

    private static async Task<IResult> RecordThirdDegreeReviewAsync(
        Guid requestId,
        ThirdDegreeReviewRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremony = await GetInitiationAsync(requestId, coreDb, cancellationToken);
        if (ceremony is null) return Results.NotFound(new { message = "La solicitud de iniciación no existe." });
        if (!access.CanManageOrganization(httpContext.User, ceremony.OrganizationId)) return Results.Forbid();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia ya está autorizada." });
        if (request.ReviewDate > ChileToday())
            return Results.BadRequest(new { message = "La revisión de tercer grado no puede registrarse con fecha futura." });

        var initialStatus = await GetLatestValidationStatusAsync(
            coreDb, requestId, CeremonyCodes.ValidationType.CandidateInitialDeliberation, cancellationToken);
        if (initialStatus != CeremonyCodes.ValidationStatus.Approved)
            return Results.Conflict(new { message = "La deliberación inicial debe estar aprobada antes de la revisión de tercer grado." });

        var hasPublication = await coreDb.CandidatePublications.AsNoTracking()
            .AnyAsync(x => x.CeremonyRequestId == requestId &&
                           (x.Status == CeremonyCodes.PublicationStatus.Published || x.Status == CeremonyCodes.PublicationStatus.Completed),
                cancellationToken);
        if (!hasPublication)
            return Results.Conflict(new { message = "La insinuación debe haber sido publicada antes de registrar la revisión de tercer grado." });

        var packageDecision = CandidateIntakeWorkflowPolicy.EvaluateInterviewPackage(
            request.CompletedInterviews,
            request.ConfidentialQuestionnaireAvailable,
            request.AutobiographyAvailable);

        var packageStatus = ToValidationStatus(packageDecision);
        var packageValidation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateInterviewPackage,
            packageStatus, request.ReviewDate, request.SourceReference, packageDecision.Reason);

        if (!packageDecision.CanProceed)
        {
            audit.Add(httpContext, "candidate.workflow.interview_package.observed", nameof(CeremonyValidation),
                packageValidation.Id.ToString(), ceremony.OrganizationId, AuditResults.Rejected,
                new
                {
                    request.CompletedInterviews,
                    request.ConfidentialQuestionnaireAvailable,
                    request.AutobiographyAvailable,
                    packageDecision.Code
                });
            await coreDb.SaveChangesAsync(cancellationToken);
            return Results.Conflict(new { packageValidation.Id, packageValidation.Status, packageDecision.Code, packageDecision.Reason });
        }

        var voteDecision = CandidateIntakeWorkflowPolicy.EvaluateThirdDegreeOpenVote(request.OpenVoteApproved);
        var voteStatus = ToValidationStatus(voteDecision);
        var voteValidation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateThirdDegreeReview,
            voteStatus, request.ReviewDate, request.SourceReference, voteDecision.Reason);

        ceremony.Status = voteDecision.IsRejected
            ? CeremonyCodes.RequestStatus.Rejected
            : CeremonyCodes.RequestStatus.UnderReview;

        audit.Add(httpContext, "candidate.workflow.third_degree_review.recorded", nameof(CeremonyValidation),
            voteValidation.Id.ToString(), ceremony.OrganizationId,
            voteDecision.IsRejected ? AuditResults.Rejected : AuditResults.Success,
            new
            {
                request.CompletedInterviews,
                request.ConfidentialQuestionnaireAvailable,
                request.AutobiographyAvailable,
                request.OpenVoteApproved,
                voteDecision.Code,
                voteStatus
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            package = new { packageValidation.Id, packageValidation.Status },
            thirdDegree = new { voteValidation.Id, voteValidation.Status, voteDecision.Code, voteDecision.Reason },
            ceremony.Status
        });
    }

    private static async Task<IResult> RecordFinalBallotAsync(
        Guid requestId,
        FinalBallotRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremony = await GetInitiationAsync(requestId, coreDb, cancellationToken);
        if (ceremony is null) return Results.NotFound(new { message = "La solicitud de iniciación no existe." });
        if (!access.CanManageOrganization(httpContext.User, ceremony.OrganizationId)) return Results.Forbid();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia ya está autorizada." });
        if (request.BallotDate > ChileToday())
            return Results.BadRequest(new { message = "El balotaje no puede registrarse con fecha futura." });

        var thirdDegreeStatus = await GetLatestValidationStatusAsync(
            coreDb, requestId, CeremonyCodes.ValidationType.CandidateThirdDegreeReview, cancellationToken);

        var publication = await coreDb.CandidatePublications.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        (x.Status == CeremonyCodes.PublicationStatus.Published || x.Status == CeremonyCodes.PublicationStatus.Completed))
            .OrderByDescending(x => x.PublishedFromUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (publication is null)
            return Results.Conflict(new { message = "No existe una publicación válida del insinuado." });

        var publicationDate = ChileDate(publication.PublishedFromUtc);
        var readiness = CandidateIntakeWorkflowPolicy.EvaluateFinalBallot(
            publicationDate,
            request.BallotDate,
            thirdDegreeStatus == CeremonyCodes.ValidationStatus.Approved,
            publication.RequiredDays);

        if (!readiness.CanProceed)
        {
            var blockedValidation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateFinalBallot,
                CeremonyCodes.ValidationStatus.Observed, request.BallotDate, request.SourceReference, readiness.Reason);
            audit.Add(httpContext, "candidate.workflow.final_ballot.blocked", nameof(CeremonyValidation),
                blockedValidation.Id.ToString(), ceremony.OrganizationId, AuditResults.Rejected,
                new { publicationDate, request.BallotDate, publication.RequiredDays, thirdDegreeStatus, readiness.Code });
            await coreDb.SaveChangesAsync(cancellationToken);
            return Results.Conflict(new { blockedValidation.Id, blockedValidation.Status, readiness.Code, readiness.Reason });
        }

        var finalStatus = request.BallotApproved
            ? CeremonyCodes.ValidationStatus.Approved
            : CeremonyCodes.ValidationStatus.Rejected;
        var finalReason = request.BallotApproved
            ? "El balotaje fue registrado como aprobado por el Taller."
            : "El balotaje fue registrado como rechazado por el Taller.";

        var validation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateFinalBallot,
            finalStatus, request.BallotDate, request.SourceReference, finalReason);

        ceremony.Status = request.BallotApproved
            ? CeremonyCodes.RequestStatus.UnderReview
            : CeremonyCodes.RequestStatus.Rejected;

        audit.Add(httpContext, "candidate.workflow.final_ballot.recorded", nameof(CeremonyValidation),
            validation.Id.ToString(), ceremony.OrganizationId,
            request.BallotApproved ? AuditResults.Success : AuditResults.Rejected,
            new
            {
                publicationDate,
                request.BallotDate,
                publication.RequiredDays,
                request.BallotApproved,
                finalStatus
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { validation.Id, validation.Status, validation.AsOfDate, ceremony.Status });
    }

    private static async Task<IResult> GetWorkflowAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var ceremony = await GetInitiationAsync(requestId, coreDb, cancellationToken, asNoTracking: true);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanReviewCeremonies(httpContext.User, ceremony.OrganizationId) &&
            !access.CanReadOrganization(httpContext.User, ceremony.OrganizationId))
            return Results.Forbid();

        var types = new[]
        {
            CeremonyCodes.ValidationType.CandidateInitialDeliberation,
            CeremonyCodes.ValidationType.CandidateInterviewPackage,
            CeremonyCodes.ValidationType.CandidateThirdDegreeReview,
            CeremonyCodes.ValidationType.CandidateFinalBallot
        };

        var validations = await coreDb.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId && types.Contains(x.ValidationType))
            .OrderByDescending(x => x.RecordedAtUtc)
            .ToListAsync(cancellationToken);

        var latest = validations
            .GroupBy(x => x.ValidationType)
            .ToDictionary(x => x.Key, x => x.First());

        var publication = await coreDb.CandidatePublications.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId)
            .OrderByDescending(x => x.PublishedFromUtc)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.PublishedFromUtc,
                x.PublishedUntilUtc,
                x.RequiredDays,
                x.RuleCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        object? Stage(string type)
            => latest.TryGetValue(type, out var item)
                ? new { item.Id, item.Status, item.AsOfDate, item.SourceReference, item.Notes, item.RecordedAtUtc }
                : null;

        return Results.Ok(new
        {
            requestId,
            ceremony.Status,
            initialDeliberation = Stage(CeremonyCodes.ValidationType.CandidateInitialDeliberation),
            interviewPackage = Stage(CeremonyCodes.ValidationType.CandidateInterviewPackage),
            thirdDegreeReview = Stage(CeremonyCodes.ValidationType.CandidateThirdDegreeReview),
            finalBallot = Stage(CeremonyCodes.ValidationType.CandidateFinalBallot),
            publication
        });
    }

    private static async Task<CeremonyRequest?> GetInitiationAsync(
        Guid requestId,
        PmgmDbContext db,
        CancellationToken cancellationToken,
        bool asNoTracking = false)
    {
        IQueryable<CeremonyRequest> query = db.CeremonyRequests;
        if (asNoTracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(
            x => x.Id == requestId && x.CeremonyType == CeremonyCodes.Type.Initiation,
            cancellationToken);
    }

    private static async Task<string?> GetLatestValidationStatusAsync(
        PmgmDbContext db,
        Guid requestId,
        string validationType,
        CancellationToken cancellationToken)
        => await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId && x.ValidationType == validationType)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(cancellationToken);

    private static CeremonyValidation AddValidation(
        PmgmDbContext db,
        Guid requestId,
        string validationType,
        string status,
        DateOnly asOfDate,
        string? sourceReference,
        string notes)
    {
        var validation = new CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = validationType,
            Status = status,
            AsOfDate = asOfDate,
            SourceReference = sourceReference,
            Notes = notes
        };
        db.CeremonyValidations.Add(validation);
        return validation;
    }

    private static string ToValidationStatus(CandidateWorkflowDecision decision)
        => decision.CanProceed
            ? CeremonyCodes.ValidationStatus.Approved
            : decision.IsRejected
                ? CeremonyCodes.ValidationStatus.Rejected
                : CeremonyCodes.ValidationStatus.Observed;

    private static DateOnly ChileToday()
    {
        var chileNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chileNow.DateTime);
    }

    private static DateOnly ChileDate(DateTimeOffset utc)
    {
        var chile = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utc, "America/Santiago");
        return DateOnly.FromDateTime(chile.DateTime);
    }
}

public sealed record InitialDeliberationRequest(
    DateOnly DeliberationDate,
    int PresentVoters,
    int VotesInFavor,
    int? MinimumWaitingDays,
    string? SourceReference);

public sealed record ThirdDegreeReviewRequest(
    DateOnly ReviewDate,
    int CompletedInterviews,
    bool ConfidentialQuestionnaireAvailable,
    bool AutobiographyAvailable,
    bool OpenVoteApproved,
    string? SourceReference);

public sealed record FinalBallotRequest(
    DateOnly BallotDate,
    bool BallotApproved,
    string? SourceReference);
