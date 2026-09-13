using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidateWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapCandidateWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/insinuados")
            .WithTags("Flujo de insinuaciones")
            .RequireAuthorization();

        group.MapPost("/solicitudes/{requestId:guid}/deliberacion-inicial", RecordInitialDeliberationAsync);
        group.MapPost("/solicitudes/{requestId:guid}/antecedentes", RecordInterviewPackageAsync);
        group.MapPut("/solicitudes/{requestId:guid}/entrevistas/{interviewId:guid}/contenido", UploadInterviewDocumentAsync);
        group.MapPost("/solicitudes/{requestId:guid}/revision-tercer-grado", RecordThirdDegreeReviewAsync);
        group.MapPost("/solicitudes/{requestId:guid}/balotaje", RecordFinalBallotAsync);
        group.MapPost("/solicitudes/{requestId:guid}/solicitud-iniciacion", SubmitInitiationRequestAsync);
        group.MapGet("/solicitudes/{requestId:guid}/flujo", GetWorkflowAsync);

        return endpoints;
    }

    private static async Task<IResult> UploadInterviewDocumentAsync(
        Guid requestId,
        Guid interviewId,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        DocumentManagementDbContext documentDb,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        IDocumentMalwareScanner scanner,
        IOptions<DocumentStorageOptions> storageOptions,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var profile = await intakeDb.CandidateIntakeProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, cancellationToken);
        if (profile is null) return Results.NotFound(new { message = "Primero debe existir la ficha privada del insinuado." });
        if (!access.CanManageOrganization(httpContext.User, profile.OrganizationId)) return Results.Forbid();
        var existingDocument = await documentDb.InstitutionalDocuments.SingleOrDefaultAsync(x => x.Id == interviewId, cancellationToken);
        if (existingDocument is not null &&
            (existingDocument.DocumentType != "candidate_interview" || existingDocument.Edition != requestId.ToString("N") || existingDocument.OrganizationId != profile.OrganizationId))
            return Results.Conflict(new { message = "El identificador de entrevista ya pertenece a otro antecedente." });

        var fileName = DecodeHeader(httpContext.Request.Headers["X-File-Name"], "entrevista.pdf");
        var interviewer = DecodeHeader(httpContext.Request.Headers["X-Interviewer"], string.Empty);
        var summary = DecodeHeader(httpContext.Request.Headers["X-Interview-Summary"], string.Empty);
        var result = httpContext.Request.Headers["X-Interview-Result"].ToString().Trim().ToLowerInvariant();
        var dateText = httpContext.Request.Headers["X-Interview-Date"].ToString();
        if (string.IsNullOrWhiteSpace(interviewer) || interviewer.Length > 240)
            return Results.BadRequest(new { message = "Debe indicar el responsable de la entrevista." });
        if (string.IsNullOrWhiteSpace(summary) || summary.Length > 1000)
            return Results.BadRequest(new { message = "El resumen es obligatorio y admite hasta 1.000 caracteres." });
        if (result is not ("favorable" or "desfavorable"))
            return Results.BadRequest(new { message = "El resultado debe ser favorable o desfavorable." });
        if (!DateOnly.TryParse(dateText, out var interviewDate) || interviewDate > ChileToday())
            return Results.BadRequest(new { message = "La fecha de entrevista no es válida." });

        var contentType = DocumentContentTypePolicy.Normalize(httpContext.Request.ContentType);
        if (!DocumentContentTypePolicy.TryValidateMetadata(fileName, contentType, out var expectedContentType, out var metadataError))
            return Results.Json(new { message = metadataError ?? "El antecedente debe ser Word o PDF." }, statusCode: StatusCodes.Status415UnsupportedMediaType);
        if (expectedContentType is not ("application/pdf" or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"))
            return Results.Json(new { message = "El antecedente de entrevista debe ser Word o PDF." }, statusCode: StatusCodes.Status415UnsupportedMediaType);
        var length = httpContext.Request.ContentLength;
        if (length is null or <= 0 || length > storageOptions.Value.MaxUploadBytes)
            return Results.Json(new { message = "El archivo está vacío o excede el tamaño permitido." }, statusCode: StatusCodes.Status413PayloadTooLarge);
        await using var buffer = new MemoryStream((int)length.Value);
        await httpContext.Request.Body.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != length.Value) return Results.BadRequest(new { message = "El tamaño recibido no coincide con el declarado." });
        var bytes = buffer.ToArray();
        if (!await DocumentContentTypePolicy.MatchesContentAsync(new MemoryStream(bytes, writable: false), expectedContentType, cancellationToken))
            return Results.Json(new { message = "La firma real del archivo no corresponde a Word o PDF." }, statusCode: StatusCodes.Status415UnsupportedMediaType);

        DocumentMalwareScanResult scan;
        try { scan = await scanner.ScanAsync(new MemoryStream(bytes, writable: false), cancellationToken); }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or InvalidDataException)
        { return Results.Json(new { message = "El análisis antivirus no está disponible; intente nuevamente." }, statusCode: StatusCodes.Status503ServiceUnavailable); }
        if (!scan.IsClean) return Results.UnprocessableEntity(new { message = "El antecedente fue rechazado por el análisis antivirus." });

        var subject = httpContext.User.FindFirst("sub")?.Value ?? httpContext.User.Identity?.Name ?? "unknown";
        var collectionCode = $"CANDIDATE-INTERVIEWS-{profile.OrganizationId:N}";
        var collection = await documentDb.DocumentCollections.SingleOrDefaultAsync(x => x.Code == collectionCode, cancellationToken);
        if (collection is null)
        {
            collection = new DocumentCollection { Code = collectionCode, Name = "Entrevistas privadas de insinuados", Description = "Antecedentes Word y PDF del circuito de Iniciación.", Scope = DocumentManagementCodes.Scope.Organization, OrganizationId = profile.OrganizationId, Status = DocumentManagementCodes.CollectionStatus.Active, CreatedBySubject = subject };
            documentDb.DocumentCollections.Add(collection);
        }
        var document = existingDocument ?? new InstitutionalDocument { Id = interviewId, Collection = collection, OrganizationId = profile.OrganizationId, Title = $"Entrevista privada {interviewId:N}", DocumentType = "candidate_interview", Classification = DocumentManagementCodes.Classification.Sensitive, AccessPolicy = DocumentManagementCodes.AccessPolicy.ManagementOnly, Edition = requestId.ToString("N"), Status = DocumentManagementCodes.DocumentStatus.Active, CreatedBySubject = subject };
        document.AuthorName = interviewer;
        document.DocumentDate = interviewDate;
        document.ShortDescription = summary;
        document.OfficialDocumentType = result;
        var nextVersion = existingDocument is null ? 1 : await documentDb.DocumentVersions.Where(x => x.DocumentId == interviewId).MaxAsync(x => x.VersionNumber, cancellationToken) + 1;
        var version = new DocumentVersion { Document = document, VersionNumber = nextVersion, OriginalFileName = fileName, ContentType = expectedContentType, SizeBytes = bytes.LongLength, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Available, ScanReference = scan.EvidenceReference, CreatedBySubject = subject };
        version.ObjectKey = DocumentObjectKeyFactory.Create(document.Id, version.Id);
        await objectStore.StoreAsync(version.ObjectKey, new MemoryStream(bytes, writable: false), expectedContentType, cancellationToken);
        if (existingDocument is null) documentDb.InstitutionalDocuments.Add(document);
        documentDb.DocumentVersions.Add(version);
        audit.Add(httpContext, "candidate.workflow.interview_document.uploaded", nameof(InstitutionalDocument), document.Id.ToString(), profile.OrganizationId, AuditResults.Success, new { requestId, interviewId, interviewDate, result, version.Id, version.SizeBytes });
        await documentDb.SaveChangesAsync(cancellationToken);
        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { interviewId, documentVersionId = version.Id, fileName, result, summary, sizeBytes = version.SizeBytes });
    }

    private static async Task<IResult> RecordInterviewPackageAsync(
        Guid requestId,
        InterviewPackageRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        DocumentManagementDbContext documentDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremony = await GetInitiationAsync(requestId, coreDb, cancellationToken);
        if (ceremony is null) return Results.NotFound(new { message = "La solicitud de iniciación no existe." });
        if (!access.CanManageOrganization(httpContext.User, ceremony.OrganizationId)) return Results.Forbid();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia ya está autorizada." });
        if (request.Interviews is null || request.Interviews.Count < 3)
            return Results.BadRequest(new { message = "Deben individualizarse al menos tres entrevistas; el Venerable Maestro puede requerir entrevistas adicionales." });
        if (request.AsOfDate > ChileToday() || request.Interviews.Any(x => x.InterviewDate > ChileToday()))
            return Results.BadRequest(new { message = "Los antecedentes no pueden registrarse con fechas futuras." });
        if (request.Interviews.Any(x => string.IsNullOrWhiteSpace(x.InterviewerDisplayName) || string.IsNullOrWhiteSpace(x.Summary) || x.Summary.Length > 1000 || x.Result is not ("favorable" or "desfavorable")))
            return Results.BadRequest(new { message = "Cada entrevista requiere responsable, resumen de hasta 1.000 caracteres y resultado favorable o desfavorable." });
        var versionIds = request.Interviews.Select(x => x.DocumentVersionId).Distinct().ToArray();
        if (versionIds.Length != request.Interviews.Count)
            return Results.BadRequest(new { message = "Cada entrevista debe vincular un antecedente documental distinto." });
        var validVersions = await documentDb.DocumentVersions.AsNoTracking()
            .Where(x => versionIds.Contains(x.Id) && x.ProcessingStatus == DocumentManagementCodes.ProcessingStatus.Available &&
                        x.Document.DocumentType == "candidate_interview" && x.Document.Edition == requestId.ToString("N") &&
                        x.Document.OrganizationId == ceremony.OrganizationId)
            .Select(x => new { x.Id, x.Document.AuthorName, x.Document.DocumentDate, x.Document.ShortDescription, x.Document.OfficialDocumentType })
            .ToListAsync(cancellationToken);
        if (validVersions.Count != request.Interviews.Count || request.Interviews.Any(item => !validVersions.Any(version =>
                version.Id == item.DocumentVersionId && version.AuthorName == item.InterviewerDisplayName.Trim() &&
                version.DocumentDate == item.InterviewDate && version.ShortDescription == item.Summary.Trim() &&
                version.OfficialDocumentType == item.Result)))
            return Results.BadRequest(new { message = "Todas las entrevistas deben tener un archivo Word o PDF validado y asociado al expediente." });
        if (request.ConfidentialQuestionnaireAvailable && string.IsNullOrWhiteSpace(request.ConfidentialQuestionnaireReference))
            return Results.BadRequest(new { message = "Debe indicar la referencia privada del cuestionario confidencial." });
        if (request.AutobiographyAvailable && string.IsNullOrWhiteSpace(request.AutobiographyReference))
            return Results.BadRequest(new { message = "Debe indicar la referencia privada de la autobiografía." });

        var initialStatus = await GetLatestValidationStatusAsync(
            coreDb, requestId, CeremonyCodes.ValidationType.CandidateInitialDeliberation, cancellationToken);
        if (initialStatus != CeremonyCodes.ValidationStatus.Approved)
            return Results.Conflict(new { message = "La deliberación inicial debe estar aprobada antes de registrar entrevistas." });
        var publication = await coreDb.CandidatePublications.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        (x.Status == CeremonyCodes.PublicationStatus.Published || x.Status == CeremonyCodes.PublicationStatus.Completed))
            .OrderByDescending(x => x.PublishedFromUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (publication is null)
            return Results.Conflict(new { message = "La insinuación debe estar publicada antes de registrar entrevistas." });
        var publicationDate = ChileDate(publication.PublishedFromUtc);
        if (request.Interviews.Any(x => x.InterviewDate < publicationDate))
            return Results.BadRequest(new { message = "Las entrevistas no pueden ser anteriores al inicio de la publicación." });

        var decision = CandidateIntakeWorkflowPolicy.EvaluateInterviewPackage(
            request.Interviews.Count,
            request.ConfidentialQuestionnaireAvailable,
            request.AutobiographyAvailable);
        var status = ToValidationStatus(decision);
        var sourceReference = string.Join(" | ", request.Interviews.Select(x => x.DocumentVersionId));
        var validation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateInterviewPackage,
            status, request.AsOfDate, sourceReference, decision.Reason);
        ceremony.Status = CeremonyCodes.RequestStatus.UnderReview;

        audit.Add(httpContext, "candidate.workflow.interview_package.recorded", nameof(CeremonyValidation),
            validation.Id.ToString(), ceremony.OrganizationId,
            decision.CanProceed ? AuditResults.Success : AuditResults.Rejected,
            new
            {
                interviews = request.Interviews.Select(x => new { x.InterviewDate, x.DocumentVersionId, x.Result }),
                request.ConfidentialQuestionnaireAvailable,
                request.ConfidentialQuestionnaireReference,
                request.AutobiographyAvailable,
                request.AutobiographyReference,
                decision.Code,
                status
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            validation.Id,
            validationStatus = validation.Status,
            decision.Code,
            decision.Reason,
            completedInterviews = request.Interviews.Count,
            ceremonyStatus = ceremony.Status
        });
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
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "Debe indicar la referencia del acta o extracto que respalda la deliberación." });
        if (request.SourceReference.Trim().Length > 240)
            return Results.BadRequest(new { message = "La referencia documental no puede superar 240 caracteres." });

        var decision = CandidateIntakeWorkflowPolicy.EvaluateInitialDeliberation(
            profile.InsinuationDate,
            request.DeliberationDate,
            request.PresentVoters,
            request.VotesInFavor,
            request.MinimumWaitingDays ?? 7);

        var status = ToValidationStatus(decision);
        var validation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateInitialDeliberation,
            status, request.DeliberationDate, request.SourceReference.Trim(), decision.Reason);

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
        return Results.Ok(new
        {
            validation.Id,
            validationStatus = validation.Status,
            decision.Code,
            decision.Reason,
            ceremonyStatus = ceremony.Status
        });
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

        var interviewPackageStatus = await GetLatestValidationStatusAsync(
            coreDb, requestId, CeremonyCodes.ValidationType.CandidateInterviewPackage, cancellationToken);
        if (interviewPackageStatus != CeremonyCodes.ValidationStatus.Approved)
            return Results.Conflict(new { message = "Las entrevistas y antecedentes deben estar validados antes de la revisión de tercer grado." });
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "Debe indicar la referencia del extracto de acta." });
        if (request.SourceReference.Trim().Length > 240)
            return Results.BadRequest(new { message = "La referencia documental no puede superar 240 caracteres." });

        var voteDecision = CandidateIntakeWorkflowPolicy.EvaluateThirdDegreeOpenVote(
            request.PresentVoters, request.VotesInFavor, request.VotesAgainst, request.Abstentions, request.OpenVoteApproved);
        var voteStatus = ToValidationStatus(voteDecision);
        var voteValidation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateThirdDegreeReview,
            voteStatus, request.ReviewDate, request.SourceReference.Trim(), voteDecision.Reason);

        ceremony.Status = voteDecision.IsRejected
            ? CeremonyCodes.RequestStatus.Rejected
            : CeremonyCodes.RequestStatus.UnderReview;

        audit.Add(httpContext, "candidate.workflow.third_degree_review.recorded", nameof(CeremonyValidation),
            voteValidation.Id.ToString(), ceremony.OrganizationId,
            voteDecision.IsRejected ? AuditResults.Rejected : AuditResults.Success,
            new
            {
                request.PresentVoters,
                request.VotesInFavor,
                request.VotesAgainst,
                request.Abstentions,
                request.OpenVoteApproved,
                voteDecision.Code,
                voteStatus
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
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
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "Debe indicar la referencia del extracto de acta." });
        if (request.SourceReference.Trim().Length > 240)
            return Results.BadRequest(new { message = "La referencia documental no puede superar 240 caracteres." });

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

        var ballotDecision = CandidateIntakeWorkflowPolicy.EvaluateFinalBallotRounds(
            request.Ballots?.Select(x => new CandidateBallotRound(x.ProcedureNumber, x.EligibleVoters, x.WhiteBallots, x.BlackBallots)).ToArray()
                ?? Array.Empty<CandidateBallotRound>(),
            request.BallotApproved);
        var finalStatus = ToValidationStatus(ballotDecision);

        var validation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateFinalBallot,
            finalStatus, request.BallotDate, request.SourceReference.Trim(), ballotDecision.Reason);

        ceremony.Status = ballotDecision.IsRejected ? CeremonyCodes.RequestStatus.Rejected : CeremonyCodes.RequestStatus.UnderReview;

        audit.Add(httpContext, "candidate.workflow.final_ballot.recorded", nameof(CeremonyValidation),
            validation.Id.ToString(), ceremony.OrganizationId,
            ballotDecision.CanProceed ? AuditResults.Success : AuditResults.Rejected,
            new
            {
                publicationDate,
                request.BallotDate,
                publication.RequiredDays,
                ballots = (request.Ballots ?? Array.Empty<FinalBallotRoundRequest>()).Select(x => new { x.ProcedureNumber, x.EligibleVoters, x.WhiteBallots, x.BlackBallots }),
                request.BallotApproved,
                finalStatus
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            validation.Id,
            validationStatus = validation.Status,
            validation.AsOfDate,
            ballotDecision.Code,
            ballotDecision.Reason,
            ceremonyStatus = ceremony.Status
        });
    }

    private static async Task<IResult> SubmitInitiationRequestAsync(
        Guid requestId,
        SubmitInitiationRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremony = await GetInitiationAsync(requestId, coreDb, cancellationToken);
        if (ceremony is null) return Results.NotFound(new { message = "La solicitud de iniciación no existe." });
        if (!access.CanManageOrganization(httpContext.User, ceremony.OrganizationId)) return Results.Forbid();
        if (ceremony.Status is CeremonyCodes.RequestStatus.Authorized or CeremonyCodes.RequestStatus.Completed)
            return Results.Conflict(new { message = "El expediente ya fue autorizado o completado." });
        if (request.SubmissionDate > ChileToday())
            return Results.BadRequest(new { message = "La solicitud formal no puede registrarse con fecha futura." });
        var submissionDecision = CandidateIntakeWorkflowPolicy.EvaluateInitiationRequestSubmission(
            request.SubmissionDate, request.ProposedCeremonyDate, request.VenerableApproval, request.SecretaryDisplayName);
        if (!submissionDecision.CanProceed)
            return Results.BadRequest(new { message = submissionDecision.Reason, submissionDecision.Code });
        if (string.IsNullOrWhiteSpace(request.SourceReference) || request.SourceReference.Trim().Length > 240)
            return Results.BadRequest(new { message = "Debe indicar una referencia documental de hasta 240 caracteres." });

        var ballotStatus = await GetLatestValidationStatusAsync(
            coreDb, requestId, CeremonyCodes.ValidationType.CandidateFinalBallot, cancellationToken);
        if (ballotStatus != CeremonyCodes.ValidationStatus.Approved)
            return Results.Conflict(new { message = "El balotaje definitivo debe estar aprobado antes de enviar la solicitud de Iniciación." });

        var existing = await coreDb.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.ValidationType == CeremonyCodes.ValidationType.CandidateCeremonySubmission &&
                        x.Status == CeremonyCodes.ValidationStatus.Approved)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return Results.Ok(new { existing.Id, validationStatus = existing.Status, ceremony.ProposedDate, ceremonyStatus = ceremony.Status, alreadySubmitted = true });

        ceremony.ProposedDate = request.ProposedCeremonyDate;
        ceremony.Status = CeremonyCodes.RequestStatus.UnderReview;
        var validation = AddValidation(coreDb, requestId, CeremonyCodes.ValidationType.CandidateCeremonySubmission,
            CeremonyCodes.ValidationStatus.Approved, request.SubmissionDate, request.SourceReference.Trim(),
            $"Solicitud confirmada por el Venerable Maestro y preparada por {request.SecretaryDisplayName.Trim()}.");

        audit.Add(httpContext, "candidate.workflow.initiation_request.submitted", nameof(CeremonyRequest),
            ceremony.Id.ToString(), ceremony.OrganizationId, AuditResults.Success,
            new { request.SubmissionDate, request.ProposedCeremonyDate, request.VenerableApproval, validation.Id });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            validation.Id,
            validationStatus = validation.Status,
            ceremony.ProposedDate,
            ceremonyStatus = ceremony.Status,
            alreadySubmitted = false
        });
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
            CeremonyCodes.ValidationType.CandidateFinalBallot,
            CeremonyCodes.ValidationType.CandidateCeremonySubmission
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
            initiationRequest = Stage(CeremonyCodes.ValidationType.CandidateCeremonySubmission),
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

    private static string DecodeHeader(string? encoded, string fallback)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return fallback;
        try { return Uri.UnescapeDataString(encoded).Trim(); }
        catch (UriFormatException) { return fallback; }
    }
}

public sealed record InitialDeliberationRequest(
    DateOnly DeliberationDate,
    int PresentVoters,
    int VotesInFavor,
    int? MinimumWaitingDays,
    string? SourceReference);

public sealed record InterviewPackageRequest(
    DateOnly AsOfDate,
    IReadOnlyList<CandidateInterviewEvidenceRequest> Interviews,
    bool ConfidentialQuestionnaireAvailable,
    string? ConfidentialQuestionnaireReference,
    bool AutobiographyAvailable,
    string? AutobiographyReference);

public sealed record CandidateInterviewEvidenceRequest(
    DateOnly InterviewDate,
    string InterviewerDisplayName,
    string Summary,
    string Result,
    Guid DocumentVersionId);

public sealed record ThirdDegreeReviewRequest(
    DateOnly ReviewDate,
    int PresentVoters,
    int VotesInFavor,
    int VotesAgainst,
    int Abstentions,
    bool OpenVoteApproved,
    string? SourceReference);

public sealed record FinalBallotRequest(
    DateOnly BallotDate,
    IReadOnlyList<FinalBallotRoundRequest>? Ballots,
    bool BallotApproved,
    string? SourceReference);

public sealed record FinalBallotRoundRequest(
    int ProcedureNumber,
    int EligibleVoters,
    int WhiteBallots,
    int BlackBallots);

public sealed record SubmitInitiationRequest(
    DateOnly SubmissionDate,
    DateOnly ProposedCeremonyDate,
    bool VenerableApproval,
    string SecretaryDisplayName,
    string SourceReference);
