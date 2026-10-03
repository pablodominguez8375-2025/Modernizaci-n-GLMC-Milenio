using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.DocumentManagement;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admisiones")
            .WithTags("Afiliaciones e incorporaciones")
            .RequireAuthorization();

        group.MapPost("/expedientes", CreateCaseAsync);
        group.MapPost("/incorporaciones/persona-nueva", AdmissionExternalIntake.CreateAsync);
        group.MapGet("/personas-busqueda", AdmissionPersonLookup.SearchAsync);
        group.MapGet("/expedientes", ListCasesAsync);
        group.MapGet("/expedientes/{caseId:guid}", GetCaseAsync);
        group.MapPost("/expedientes/{caseId:guid}/evidencias", AddEvidenceAsync);
        group.MapPost("/expedientes/{caseId:guid}/evidencias/{evidenceId:guid}/revision", ReviewEvidenceAsync);
        group.MapPost("/expedientes/{caseId:guid}/verificaciones/carta-retiro-firma-manuscrita", VerifyWithdrawalLetterSignatureAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/gran-maestria-aceptacion-especial", RecordGrandMasterSpecialAcceptanceAsync);
        group.MapPost("/expedientes/{caseId:guid}/carta-retiro/correccion-fecha", AdmissionWithdrawalDateCorrection.CorrectAsync);
        group.MapGet("/expedientes/{caseId:guid}/elegibilidad", GetEligibilityAsync);

        return endpoints;
    }

    private static async Task<IResult> ListCasesAsync(
        Guid? organizationId,
        string? status,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (organizationId is null && !access.CanManageGrandSecretariat(httpContext.User))
            return Results.BadRequest(new { message = "Debe indicar el Taller para consultar expedientes." });
        if (organizationId is { } scopedOrganization && !access.CanManageOrganization(httpContext.User, scopedOrganization) &&
            !access.CanManageGrandSecretariat(httpContext.User))
            return Results.Forbid();
        var query = admissionsDb.AdmissionCases.AsNoTracking();
        if (organizationId is { } organization) query = query.Where(x => x.OrganizationId == organization);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Take(200)
            .Select(x => new
            {
                x.Id, x.OrganizationId, x.AdmissionType, x.AffiliationMode,
                x.WithdrawalLetterGrantedDate, x.Status, x.CreatedAtUtc,
                EvidenceCount = x.Evidence.Count(),
                LatestEvidenceStatus = x.Evidence.OrderByDescending(e => e.CreatedAtUtc).Select(e => e.ReviewStatus).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> CreateCaseAsync(
        CreateAdmissionCaseRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (request.AdmissionType is not CeremonyCodes.Type.Affiliation and not CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "El tipo de expediente debe ser afiliación o incorporación." });

        if (!access.CanManageOrganization(httpContext.User, request.OrganizationId) &&
            !access.CanEvaluateCeremonies(httpContext.User))
            return Results.Forbid();

        var identityFailure = await AdmissionIdentityGuard.ValidateAsync(request,
            access.CanEvaluateCeremonies(httpContext.User), coreDb, admissionsDb, cancellationToken);
        if (identityFailure is not null) return identityFailure;

        var today = ChileToday();
        if (request.PreviousRejectionDate > today)
            return Results.BadRequest(new { message = "La fecha de rechazo anterior no puede estar en el futuro." });

        if (request.AdmissionType == CeremonyCodes.Type.Affiliation)
        {
            if (!AdmissionCodes.AffiliationMode.IsValid(request.AffiliationMode))
                return Results.BadRequest(new { message = "La afiliación debe indicar modalidad simple o con activación." });
            if (request.WithdrawalLetterGrantedDate is null)
                return Results.BadRequest(new { message = "La afiliación debe indicar la fecha de otorgamiento de la Carta de Retiro Voluntario." });
            if (request.WithdrawalLetterGrantedDate > today)
                return Results.BadRequest(new { message = "La fecha de la Carta de Retiro Voluntario no puede estar en el futuro." });
            var expectedMode = WithdrawalLetterPolicy.ModeAtCreation(request.WithdrawalLetterGrantedDate, today);
            if (!string.Equals(request.AffiliationMode, expectedMode, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { message = $"La antigüedad de la Carta de Retiro exige afiliación {expectedMode}." });
            if (request.MemberId is null)
                return Results.BadRequest(new { message = "Una afiliación requiere un hermano ya registrado en la base maestra." });

            var member = await coreDb.Members.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == request.MemberId.Value, cancellationToken);
            if (member is null)
                return Results.NotFound(new { message = "El hermano indicado no existe." });
            if (member.PersonId != request.PersonId)
                return Results.BadRequest(new { message = "El hermano y la persona indicados no corresponden al mismo registro maestro." });
        }
        else
        {
            if (request.MemberId is not null)
                return Results.BadRequest(new { message = "Una incorporación desde otra Obediencia no debe crear ni exigir membresía GLMCh antes de su resolución." });
            if (!string.IsNullOrWhiteSpace(request.AffiliationMode))
                return Results.BadRequest(new { message = "La modalidad simple/con activación sólo aplica a afiliaciones." });
            if (string.IsNullOrWhiteSpace(request.OriginObedience))
                return Results.BadRequest(new { message = "La incorporación debe registrar la Obediencia de origen." });
            if (string.IsNullOrWhiteSpace(request.Degree))
                return Results.BadRequest(new { message = "La incorporación debe registrar el grado masónico declarado para su posterior acreditación documental." });
        }

        var entity = new AdmissionCase
        {
            OrganizationId = request.OrganizationId,
            AdmissionType = request.AdmissionType,
            AffiliationMode = Normalize(request.AffiliationMode),
            WithdrawalLetterGrantedDate = request.WithdrawalLetterGrantedDate,
            MemberId = request.MemberId,
            PersonId = request.PersonId,
            OriginOrganizationId = request.OriginOrganizationId,
            OriginLodgeName = Normalize(request.OriginLodgeName),
            OriginLodgeNumber = Normalize(request.OriginLodgeNumber),
            OriginObedience = Normalize(request.OriginObedience),
            Degree = Normalize(request.Degree),
            WageIncreaseEvidenceApplies = request.AdmissionType == CeremonyCodes.Type.Incorporation && request.WageIncreaseEvidenceApplies,
            ExaltationEvidenceApplies = request.AdmissionType == CeremonyCodes.Type.Incorporation && request.ExaltationEvidenceApplies,
            HasPeaceAndFriendshipPact = request.AdmissionType == CeremonyCodes.Type.Incorporation ? request.HasPeaceAndFriendshipPact : null,
            PreviousRejectionDate = request.PreviousRejectionDate,
            RejectionCausesRemedied = request.RejectionCausesRemedied,
            Status = AdmissionWorkflowCodes.CaseStatus.UnderReview,
            CreatedBySubject = GetSubject(httpContext.User)
        };

        admissionsDb.AdmissionCases.Add(entity);
        await admissionsDb.SaveChangesAsync(cancellationToken);

        audit.Add(httpContext, "admission.case.created", nameof(AdmissionCase), entity.Id.ToString(), entity.OrganizationId,
            AuditResults.Success, new
            {
                entity.AdmissionType,
                entity.AffiliationMode,
                entity.HasPeaceAndFriendshipPact,
                entity.Status
            });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admisiones/expedientes/{entity.Id}", ToCaseDto(entity));
    }

    private static async Task<IResult> GetCaseAsync(
        Guid caseId,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var entity = await admissionsDb.AdmissionCases.AsNoTracking()
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (entity is null) return Results.NotFound();

        if (!access.CanReadOrganization(httpContext.User, entity.OrganizationId) &&
            !access.CanEvaluateCeremonies(httpContext.User))
            return Results.Forbid();

        return Results.Ok(new
        {
            admissionCase = ToCaseDto(entity),
            evidence = entity.Evidence.OrderByDescending(x => x.CreatedAtUtc).Select(ToEvidenceDto),
            decisions = entity.Decisions.OrderByDescending(x => x.RecordedAtUtc).Select(ToDecisionDto)
        });
    }

    private static async Task<IResult> AddEvidenceAsync(
        Guid caseId,
        AddAdmissionEvidenceRequest request,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        DocumentManagementDbContext documentDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!AdmissionWorkflowCodes.EvidenceType.IsValid(request.EvidenceType))
            return Results.BadRequest(new { message = "El tipo de antecedente documental no es válido." });

        var admissionCase = await admissionsDb.AdmissionCases.SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, admissionCase.OrganizationId) &&
            !access.CanManageGrandSecretariat(httpContext.User))
            return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya está resuelto y no admite nuevos antecedentes por este flujo." });

        if (request.DocumentVersionId is null)
            return Results.BadRequest(new { message = "Los antecedentes de afiliación/incorporación deben vincularse a una versión documental trazable." });

        var version = await documentDb.DocumentVersions.AsNoTracking()
            .Include(x => x.Document)
            .SingleOrDefaultAsync(x => x.Id == request.DocumentVersionId.Value, cancellationToken);
        if (version is null)
            return Results.NotFound(new { message = "La versión documental indicada no existe." });
        if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.Available)
            return Results.Conflict(new { message = "El documento debe completar su carga y análisis de seguridad antes de incorporarse al expediente." });
        if (version.Document.OrganizationId is not null && version.Document.OrganizationId != admissionCase.OrganizationId)
            return Results.BadRequest(new { message = "El documento pertenece a un ámbito de Taller distinto del expediente." });

        var evidence = new AdmissionEvidence
        {
            AdmissionCaseId = caseId,
            EvidenceType = request.EvidenceType,
            DocumentVersionId = request.DocumentVersionId,
            EvidenceDate = request.EvidenceDate,
            SourceReference = Normalize(request.SourceReference),
            ReviewStatus = CeremonyCodes.ValidationStatus.Pending,
            Notes = Normalize(request.Notes),
            CreatedBySubject = GetSubject(httpContext.User)
        };

        admissionsDb.AdmissionEvidence.Add(evidence);
        await admissionsDb.SaveChangesAsync(cancellationToken);

        audit.Add(httpContext, "admission.evidence.added", nameof(AdmissionEvidence), evidence.Id.ToString(), admissionCase.OrganizationId,
            AuditResults.Success, new { evidence.EvidenceType, evidence.ReviewStatus });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/admisiones/expedientes/{caseId}/evidencias/{evidence.Id}", ToEvidenceDto(evidence));
    }

    private static async Task<IResult> ReviewEvidenceAsync(
        Guid caseId,
        Guid evidenceId,
        AdmissionReviewRequest request,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();
        if (!IsReviewStatusValid(request.Status))
            return Results.BadRequest(new { message = "La revisión debe aprobar, observar o rechazar el antecedente." });

        var evidence = await admissionsDb.AdmissionEvidence
            .Include(x => x.AdmissionCase)
            .SingleOrDefaultAsync(x => x.Id == evidenceId && x.AdmissionCaseId == caseId, cancellationToken);
        if (evidence is null) return Results.NotFound();

        if (evidence.AdmissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.AsOfDate > ChileToday())
            return Results.BadRequest(new { message = "La revisión no puede registrarse con fecha futura." });
        var now = DateTimeOffset.UtcNow;
        var subject = GetSubject(httpContext.User);
        evidence.ReviewStatus = request.Status;
        evidence.ReviewedBySubject = subject;
        evidence.ReviewedAtUtc = now;

        var decision = new AdmissionDecision
        {
            AdmissionCaseId = caseId,
            DecisionType = AdmissionWorkflowCodes.DecisionType.EvidenceReview(evidenceId),
            Status = request.Status,
            AsOfDate = request.AsOfDate ?? ChileToday(),
            SourceReference = Normalize(request.SourceReference),
            Notes = Normalize(request.Notes),
            RecordedBySubject = subject
        };
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(cancellationToken);

        audit.Add(httpContext, "admission.evidence.reviewed", nameof(AdmissionEvidence), evidence.Id.ToString(), evidence.AdmissionCase.OrganizationId,
            request.Status == CeremonyCodes.ValidationStatus.Approved ? AuditResults.Success : AuditResults.Observed,
            new { evidence.EvidenceType, reviewStatus = request.Status });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { evidence = ToEvidenceDto(evidence), decision = ToDecisionDto(decision) });
    }

    private static async Task<IResult> VerifyWithdrawalLetterSignatureAsync(
        Guid caseId,
        WithdrawalLetterSignatureReviewRequest request,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();
        if (!IsReviewStatusValid(request.Status))
            return Results.BadRequest(new { message = "La verificación debe aprobar, observar o rechazar." });

        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().Include(x => x.Evidence)
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (admissionCase is null) return Results.NotFound();

        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.EvidenceId is null || request.AsOfDate is null || request.AsOfDate > ChileToday() ||
            string.IsNullOrWhiteSpace(request.SourceReference) || request.SourceReference.Length > 500 || request.Notes?.Length > 4000)
            return Results.BadRequest(new { message = "Indique la carta concreta, fecha de revisión no futura y fuente institucional." });
        var letter = AdmissionWithdrawalEvidencePolicy.CurrentLetter(admissionCase);
        if (letter is null || letter.Id != request.EvidenceId || letter.DocumentVersionId is null)
            return Results.Conflict(new { message = "Debe revisar la versión trazable de la carta más reciente de este expediente." });
        if (request.Status == CeremonyCodes.ValidationStatus.Approved &&
            (letter.ReviewStatus != CeremonyCodes.ValidationStatus.Approved || letter.ReviewedAtUtc is null ||
             !AdmissionWithdrawalEvidencePolicy.HasValidDate(admissionCase, letter, ChileToday()) || request.AsOfDate < letter.EvidenceDate))
            return Results.Conflict(new { message = "La carta debe estar aprobada, con fecha de otorgamiento acreditada y coherente con la modalidad del expediente." });

        var decision = CreateDecision(caseId,
            AdmissionWorkflowCodes.DecisionType.WithdrawalSignature(letter.Id),
            new AdmissionReviewRequest(request.Status, request.AsOfDate, request.SourceReference, request.Notes),
            GetSubject(httpContext.User));
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(cancellationToken);

        audit.Add(httpContext, "admission.withdrawal_letter.handwritten_signature_verified", nameof(AdmissionDecision),
            decision.Id.ToString(), admissionCase.OrganizationId,
            request.Status == CeremonyCodes.ValidationStatus.Approved ? AuditResults.Success : AuditResults.Observed,
            new { decision.Status, decision.AsOfDate, evidenceId = letter.Id, letter.DocumentVersionId });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDecisionDto(decision));
    }

    private static async Task<IResult> RecordGrandMasterSpecialAcceptanceAsync(
        Guid caseId,
        AdmissionReviewRequest request,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanProvideGrandMasterApproval(httpContext.User)) return Results.Forbid();
        if (!IsReviewStatusValid(request.Status))
            return Results.BadRequest(new { message = "La decisión de Gran Maestría debe aprobar, observar o rechazar." });

        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (admissionCase is null) return Results.NotFound();
        if (admissionCase.AdmissionType != CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "La aceptación especial de Gran Maestría sólo aplica a incorporaciones desde otra Obediencia." });
        if (admissionCase.HasPeaceAndFriendshipPact != false)
            return Results.Conflict(new { message = "Esta decisión especial se registra cuando consta que no existe Pacto de Paz y Amistad con la Obediencia de origen." });

        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.AsOfDate is null || request.AsOfDate > ChileToday() || string.IsNullOrWhiteSpace(request.SourceReference) || request.SourceReference.Length > 500)
            return Results.BadRequest(new { message = "Indique fecha no futura y resolución institucional de Gran Maestría." });

        var decision = CreateDecision(caseId,
            AdmissionWorkflowCodes.DecisionType.GrandMasterSpecialAcceptance,
            request,
            GetSubject(httpContext.User));
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(cancellationToken);

        audit.Add(httpContext, "admission.grand_master.special_acceptance.recorded", nameof(AdmissionDecision),
            decision.Id.ToString(), admissionCase.OrganizationId,
            request.Status == CeremonyCodes.ValidationStatus.Approved ? AuditResults.Success : AuditResults.Rejected,
            new { decision.Status, decision.AsOfDate });
        await coreDb.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDecisionDto(decision));
    }

    private static async Task<IResult> GetEligibilityAsync(
        Guid caseId,
        HttpContext httpContext,
        AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking()
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (admissionCase is null) return Results.NotFound();

        if (!access.CanReadOrganization(httpContext.User, admissionCase.OrganizationId) &&
            !access.CanEvaluateCeremonies(httpContext.User))
            return Results.Forbid();

        var withdrawalLetter = AdmissionWithdrawalEvidencePolicy.CurrentLetter(admissionCase);
        var initiationEvidence = LatestApprovedEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedInitiation);
        var wageEvidence = LatestApprovedEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedWageIncrease);
        var exaltationEvidence = LatestApprovedEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.LegalizedExaltation);
        var degreeEvidence = LatestApprovedEvidence(admissionCase, AdmissionWorkflowCodes.EvidenceType.Degree);

        var signatureDecision = AdmissionWithdrawalEvidencePolicy.VerifiedSignature(admissionCase, ChileToday());
        var gmSpecialDecision = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.GrandMasterSpecialAcceptance);

        var decision = AdmissionCaseEligibilityProjector.Evaluate(admissionCase).Decision;

        return Results.Ok(new
        {
            caseId,
            admissionCase.AdmissionType,
            admissionCase.Status,
            eligibility = decision,
            evidence = new
            {
                withdrawalLetterId = withdrawalLetter?.Id,
                legalizedInitiationEvidenceId = initiationEvidence?.Id,
                legalizedWageIncreaseEvidenceId = wageEvidence?.Id,
                legalizedExaltationEvidenceId = exaltationEvidence?.Id,
                degreeEvidenceId = degreeEvidence?.Id,
                handwrittenSignatureDecisionId = signatureDecision?.Id,
                grandMasterSpecialAcceptanceDecisionId = gmSpecialDecision?.Id
            }
        });
    }

    private static AdmissionEvidence? LatestEvidence(AdmissionCase admissionCase, string evidenceType)
        => admissionCase.Evidence
            .Where(x => x.EvidenceType == evidenceType && x.ReviewStatus != CeremonyCodes.ValidationStatus.Rejected)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

    private static AdmissionEvidence? LatestApprovedEvidence(AdmissionCase admissionCase, string evidenceType)
        => admissionCase.Evidence
            .Where(x => x.EvidenceType == evidenceType && x.ReviewStatus == CeremonyCodes.ValidationStatus.Approved)
            .OrderByDescending(x => x.ReviewedAtUtc ?? x.CreatedAtUtc)
            .FirstOrDefault();

    private static AdmissionDecision? LatestDecision(AdmissionCase admissionCase, string decisionType)
        => admissionCase.Decisions
            .Where(x => x.DecisionType == decisionType)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefault();

    private static AdmissionDecision CreateDecision(
        Guid caseId,
        string decisionType,
        AdmissionReviewRequest request,
        string subject)
        => new()
        {
            AdmissionCaseId = caseId,
            DecisionType = decisionType,
            Status = request.Status,
            AsOfDate = request.AsOfDate ?? ChileToday(),
            SourceReference = Normalize(request.SourceReference),
            Notes = Normalize(request.Notes),
            RecordedBySubject = subject
        };

    private static bool IsReviewStatusValid(string value)
        => value is CeremonyCodes.ValidationStatus.Approved
            or CeremonyCodes.ValidationStatus.Observed
            or CeremonyCodes.ValidationStatus.Rejected;

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly ChileToday()
    {
        var chileNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chileNow.DateTime);
    }

    private static DateOnly ChileDate(DateTimeOffset instant)
    {
        var chileNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(instant, "America/Santiago");
        return DateOnly.FromDateTime(chileNow.DateTime);
    }

    private static object ToCaseDto(AdmissionCase entity) => new
    {
        entity.Id,
        entity.OrganizationId,
        entity.AdmissionType,
        entity.AffiliationMode,
        entity.WithdrawalLetterGrantedDate,
        entity.MemberId,
        entity.PersonId,
        entity.OriginOrganizationId,
        entity.OriginLodgeName,
        entity.OriginLodgeNumber,
        entity.OriginObedience,
        entity.Degree,
        entity.WageIncreaseEvidenceApplies,
        entity.ExaltationEvidenceApplies,
        entity.HasPeaceAndFriendshipPact,
        entity.PreviousRejectionDate,
        entity.RejectionCausesRemedied,
        entity.Status,
        entity.CreatedAtUtc
    };

    private static object ToEvidenceDto(AdmissionEvidence entity) => new
    {
        entity.Id,
        entity.AdmissionCaseId,
        entity.EvidenceType,
        entity.DocumentVersionId,
        entity.EvidenceDate,
        entity.SourceReference,
        entity.ReviewStatus,
        entity.ReviewedAtUtc,
        entity.Notes,
        entity.CreatedAtUtc
    };

    private static object ToDecisionDto(AdmissionDecision entity) => new
    {
        entity.Id,
        entity.AdmissionCaseId,
        entity.DecisionType,
        entity.Status,
        entity.AsOfDate,
        entity.SourceReference,
        entity.Notes,
        entity.RecordedAtUtc
    };
}

public sealed record CreateAdmissionCaseRequest(
    Guid OrganizationId,
    string AdmissionType,
    string? AffiliationMode,
    Guid? MemberId,
    Guid PersonId,
    Guid? OriginOrganizationId,
    string? OriginLodgeName,
    string? OriginLodgeNumber,
    string? OriginObedience,
    string? Degree,
    bool WageIncreaseEvidenceApplies,
    bool ExaltationEvidenceApplies,
    bool? HasPeaceAndFriendshipPact,
    DateOnly? PreviousRejectionDate,
    bool? RejectionCausesRemedied,
    DateOnly? WithdrawalLetterGrantedDate = null);

public sealed record AddAdmissionEvidenceRequest(
    string EvidenceType,
    Guid? DocumentVersionId,
    DateOnly? EvidenceDate,
    string? SourceReference,
    string? Notes);

public sealed record AdmissionReviewRequest(
    string Status,
    DateOnly? AsOfDate,
    string? SourceReference,
    string? Notes);

public sealed record WithdrawalLetterSignatureReviewRequest(
    string Status, DateOnly? AsOfDate, string? SourceReference, string? Notes, Guid? EvidenceId = null);
