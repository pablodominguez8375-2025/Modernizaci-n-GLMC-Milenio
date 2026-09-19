using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionCeremonyEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionCeremonyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admisiones")
            .WithTags("Afiliaciones e incorporaciones - solicitud de ceremonia")
            .RequireAuthorization();

        group.MapPost("/expedientes/{caseId:guid}/solicitud-ceremonia", CreateCeremonyRequestAsync);
        group.MapPost("/ceremonias/{requestId:guid}/registrar-realizacion", CompleteAdmissionCeremonyAsync);
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
            .Include(x => x.CommissionAppointments)
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

    private static async Task<IResult> CompleteAdmissionCeremonyAsync(
        Guid requestId,
        CompleteAdmissionCeremonyRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        AdmissionsDbContext admissionsDb,
        GrandSecretariatDbContext secretariatDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremony = await coreDb.CeremonyRequests
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null || ceremony.AdmissionCaseId is null)
            return Results.NotFound(new { message = "La ceremonia de afiliación/incorporación no existe." });

        if (ceremony.CeremonyType is not CeremonyCodes.Type.Affiliation and not CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "Esta operación sólo corresponde a Afiliación o Incorporación." });

        var admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .Include(x => x.CommissionAppointments)
            .SingleOrDefaultAsync(x => x.Id == ceremony.AdmissionCaseId.Value, cancellationToken);
        if (admissionCase is null)
            return Results.Conflict(new { message = "La ceremonia no tiene un expediente de admisión disponible." });

        if (!access.CanManageOrganization(httpContext.User, admissionCase.OrganizationId))
            return Results.Forbid();

        if (ceremony.Status == CeremonyCodes.RequestStatus.Completed)
        {
            await ReconcileCompletionAsync(admissionCase, ceremony, request, admissionsDb, cancellationToken);
            return Results.Ok(new
            {
                ceremony.Id,
                ceremony.Status,
                ceremony.MemberId,
                admissionCaseId = admissionCase.Id,
                admissionStatus = admissionCase.Status,
                alreadyCompleted = true
            });
        }

        if (ceremony.Status != CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia debe estar institucionalmente autorizada antes de registrar su realización." });
        if (request.CeremonyDate > ChileToday())
            return Results.BadRequest(new { message = "La fecha de la ceremonia no puede estar en el futuro." });
        if (string.IsNullOrWhiteSpace(request.MinuteReference))
            return Results.BadRequest(new { message = "La referencia del acta/extracto de la ceremonia es obligatoria." });

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);
        if (!projection.Decision.CanProceed)
            return Results.Conflict(new { message = "El expediente dejó de estar habilitado y no puede materializar la admisión.", eligibility = projection.Decision });

        var authorization = await secretariatDb.SecretariatDocuments.AsNoTracking()
            .Where(x => x.RelatedCeremonyRequestId == requestId &&
                        ((x.DocumentType == GrandSecretariatCodes.DocumentType.Plancha &&
                          x.PlanchaKind == GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization) ||
                         x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationLegacy ||
                         x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationPlanchaLegacy) &&
                        x.Status == GrandSecretariatCodes.DocumentStatus.Issued)
            .OrderByDescending(x => x.IssuedAtUtc)
            .Select(x => new { x.DocumentCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (authorization is null)
            return Results.Conflict(new { message = "No puede registrarse la admisión sin una Plancha de Autorización vigente emitida por Gran Secretaría." });

        var evidenceReference = $"{authorization.DocumentCode}; {request.MinuteReference.Trim()}; admission:{admissionCase.Id}";
        Guid memberId;

        if (admissionCase.AdmissionType == CeremonyCodes.Type.Affiliation)
        {
            if (admissionCase.MemberId is null)
                return Results.Conflict(new { message = "La Afiliación no tiene un hermano maestro vinculado." });

            memberId = admissionCase.MemberId.Value;
            var duplicate = await coreDb.Memberships.AnyAsync(
                x => x.MemberId == memberId &&
                     x.OrganizationId == admissionCase.OrganizationId &&
                     x.Status == MembershipCodes.MembershipStatus.Active &&
                     x.EndDate == null,
                cancellationToken);
            if (duplicate)
                return Results.Conflict(new { message = "El hermano ya registra una pertenencia activa al Taller destino." });

            coreDb.Memberships.Add(new PMGM.Api.Modules.Membership.Entities.Membership
            {
                MemberId = memberId,
                OrganizationId = admissionCase.OrganizationId,
                MembershipType = "regular",
                StartDate = request.CeremonyDate,
                Status = MembershipCodes.MembershipStatus.Active,
                EvidenceReference = evidenceReference
            });
            coreDb.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent
            {
                MemberId = memberId,
                OrganizationId = admissionCase.OrganizationId,
                EventType = MembershipCodes.InstitutionalStatus.Active,
                EffectiveDate = request.CeremonyDate,
                EvidenceReference = evidenceReference,
                Reason = admissionCase.AffiliationMode == AdmissionCodes.AffiliationMode.Activation
                    ? "Activación por ceremonia de Afiliación realizada."
                    : "Ingreso al Cuadro por ceremonia de Afiliación realizada."
            });
            ceremony.MemberId = memberId;
        }
        else
        {
            if (await coreDb.Members.AnyAsync(x => x.PersonId == admissionCase.PersonId, cancellationToken))
                return Results.Conflict(new { message = "La persona ya fue materializada como Miembro GLMCh; revise el expediente antes de continuar." });

            var degree = NormalizeDegree(admissionCase.Degree);
            if (degree is null)
                return Results.Conflict(new { message = "El grado acreditado no puede convertirse a un grado institucional Aprendiz/Compañero/Maestro." });

            var member = new Member
            {
                PersonId = admissionCase.PersonId,
                CurrentDegree = degree
            };
            coreDb.Members.Add(member);
            coreDb.Memberships.Add(new PMGM.Api.Modules.Membership.Entities.Membership
            {
                Member = member,
                OrganizationId = admissionCase.OrganizationId,
                MembershipType = "regular",
                StartDate = request.CeremonyDate,
                Status = MembershipCodes.MembershipStatus.Active,
                EvidenceReference = evidenceReference
            });
            coreDb.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent
            {
                Member = member,
                OrganizationId = admissionCase.OrganizationId,
                EventType = MembershipCodes.InstitutionalStatus.Active,
                EffectiveDate = request.CeremonyDate,
                EvidenceReference = evidenceReference,
                Reason = "Ingreso al Cuadro por ceremonia de Incorporación realizada."
            });
            ceremony.Member = member;
            memberId = member.Id;
        }

        ceremony.Status = CeremonyCodes.RequestStatus.Completed;
        audit.Add(
            httpContext,
            "admission.ceremony.completed",
            nameof(CeremonyRequest),
            ceremony.Id.ToString(),
            ceremony.OrganizationId,
            AuditResults.Success,
            new
            {
                ceremony.CeremonyType,
                memberId,
                request.CeremonyDate,
                authorization.DocumentCode,
                admissionCaseId = admissionCase.Id
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        await ReconcileCompletionAsync(admissionCase, ceremony, request, admissionsDb, cancellationToken);

        return Results.Ok(new
        {
            ceremony.Id,
            ceremony.Status,
            memberId,
            admissionCaseId = admissionCase.Id,
            admissionStatus = admissionCase.Status,
            effectiveDate = request.CeremonyDate,
            authorization.DocumentCode,
            alreadyCompleted = false
        });
    }

    private static async Task ReconcileCompletionAsync(
        AdmissionCase admissionCase,
        CeremonyRequest ceremony,
        CompleteAdmissionCeremonyRequest request,
        AdmissionsDbContext admissionsDb,
        CancellationToken cancellationToken)
    {
        admissionCase.Status = AdmissionWorkflowCodes.CaseStatus.Resolved;
        var reference = ceremony.Id.ToString();
        var alreadyRecorded = admissionCase.Decisions.Any(x =>
            x.DecisionType == AdmissionWorkflowCodes.DecisionType.CeremonyCompleted &&
            x.SourceReference == reference);

        if (!alreadyRecorded)
        {
            admissionsDb.AdmissionDecisions.Add(new AdmissionDecision
            {
                AdmissionCaseId = admissionCase.Id,
                DecisionType = AdmissionWorkflowCodes.DecisionType.CeremonyCompleted,
                Status = CeremonyCodes.ValidationStatus.Approved,
                AsOfDate = request.CeremonyDate,
                SourceReference = reference,
                Notes = Normalize(request.MinuteReference),
                StructuredDataJson = JsonSerializer.Serialize(new
                {
                    ceremonyRequestId = ceremony.Id,
                    ceremony.CeremonyType,
                    ceremony.MemberId,
                    request.CeremonyDate
                }),
                RecordedBySubject = admissionCase.CreatedBySubject
            });
        }

        await admissionsDb.SaveChangesAsync(cancellationToken);
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

    private static string? NormalizeDegree(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }
        var degree = builder.ToString();
        return degree switch
        {
            "apprentice" or "aprendiz" or "1" or "primer grado" => "apprentice",
            "fellowcraft" or "companero" or "2" or "segundo grado" => "fellowcraft",
            "master" or "maestro" or "3" or "tercer grado" => "master",
            _ => null
        };
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

public sealed record CompleteAdmissionCeremonyRequest(
    DateOnly CeremonyDate,
    string MinuteReference);
