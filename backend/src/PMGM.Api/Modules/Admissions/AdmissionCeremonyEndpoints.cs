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
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.SecretariatOperations;
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

        if (!access.CanManageLodgeSecretariat(httpContext.User, admissionCase.OrganizationId))
            return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya se encuentra resuelto." });
        if (request.Notes?.Length > 2000)
            return Results.BadRequest(new { message = "Las observaciones no pueden exceder 2000 caracteres." });

        var existing = await coreDb.CeremonyRequests
            .SingleOrDefaultAsync(x => x.AdmissionCaseId == caseId, cancellationToken);
        if (existing is not null)
        {
            var recordedBySubject = await FindAuditActorAsync(
                coreDb,
                "admission.ceremony_request.created",
                nameof(CeremonyRequest),
                existing.Id,
                cancellationToken) ?? GetSubject(httpContext.User);
            await ReconcileCaseLinkAsync(admissionCase, existing.Id, recordedBySubject, admissionsDb, cancellationToken);
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
        await ReconcileCaseLinkAsync(
            admissionCase,
            ceremony.Id,
            GetSubject(httpContext.User),
            admissionsDb,
            cancellationToken);

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
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremonySnapshot = await coreDb.CeremonyRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremonySnapshot is null || ceremonySnapshot.AdmissionCaseId is null)
            return Results.NotFound(new { message = "La ceremonia de afiliación/incorporación no existe." });

        if (ceremonySnapshot.CeremonyType is not CeremonyCodes.Type.Affiliation and not CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "Esta operación sólo corresponde a Afiliación o Incorporación." });

        var admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .Include(x => x.CommissionAppointments)
            .SingleOrDefaultAsync(x => x.Id == ceremonySnapshot.AdmissionCaseId.Value, cancellationToken);
        if (admissionCase is null)
            return Results.Conflict(new { message = "La ceremonia no tiene un expediente de admisión disponible." });

        if (!access.CanManageLodgeSecretariat(httpContext.User, admissionCase.OrganizationId))
            return Results.Forbid();

        if (request.CeremonyDate > ChileToday())
            return Results.BadRequest(new { message = "La fecha de la ceremonia no puede estar en el futuro." });

        var meeting = await lodgeDb.LodgeMeetings.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == request.MeetingId && x.OrganizationId == admissionCase.OrganizationId,
                cancellationToken);
        if (meeting is null)
            return Results.NotFound(new { message = "La Tenida ceremonial vinculada no existe en el Taller." });
        if (meeting.Status != LodgeManagementCodes.MeetingStatus.Closed || meeting.ClosedAtUtc is null)
            return Results.Conflict(new { message = "La Tenida ceremonial debe estar cerrada documentalmente antes de materializar la admisión. El cierre exige Extracto de Acta y Plancha de Autorización." });
        if (!string.Equals(meeting.CeremonyType, ceremonySnapshot.CeremonyType, StringComparison.Ordinal))
            return Results.Conflict(new { message = "El tipo de ceremonia de la Tenida no corresponde al expediente de admisión." });
        if (meeting.MeetingDate != request.CeremonyDate)
            return Results.BadRequest(new { message = "La fecha informada debe coincidir con la fecha real de la Tenida ceremonial." });
        if (ceremonySnapshot.ProposedDate is not null && ceremonySnapshot.ProposedDate.Value != meeting.MeetingDate)
            return Results.Conflict(new { message = "La Tenida no coincide con la fecha autorizada en la solicitud de ceremonia." });

        var record = await coreDb.LodgeSecretariatRecords.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.RecordType == SecretariatOperationsCodes.RecordType.LodgeMeeting &&
                     x.SourceRecordId == meeting.Id &&
                     x.OrganizationId == admissionCase.OrganizationId,
                cancellationToken);
        if (record?.ExtractDocumentVersionId is null)
            return Results.Conflict(new { message = "La Tenida ceremonial cerrada debe conservar el Extracto de Acta adjunto." });
        if (record.CeremonyAuthorizationDocumentId is null)
            return Results.Conflict(new { message = "La Tenida ceremonial cerrada debe conservar la Plancha de Autorización de Gran Secretaría adjunta." });

        var authorization = await secretariatDb.SecretariatDocuments.AsNoTracking()
            .Where(x => x.Id == record.CeremonyAuthorizationDocumentId.Value &&
                        x.OrganizationId == admissionCase.OrganizationId &&
                        x.RelatedCeremonyRequestId == requestId &&
                        ((x.DocumentType == GrandSecretariatCodes.DocumentType.Plancha &&
                          x.PlanchaKind == GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization) ||
                         x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationLegacy ||
                         x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorizationPlanchaLegacy) &&
                        x.Status == GrandSecretariatCodes.DocumentStatus.Issued)
            .Select(x => new { x.Id, x.DocumentCode })
            .SingleOrDefaultAsync(cancellationToken);
        if (authorization is null)
            return Results.Conflict(new { message = "La Plancha adjunta no es la autorización vigente emitida por Gran Secretaría para esta ceremonia." });

        await using var coreTransaction = await coreDb.Database.BeginTransactionAsync(cancellationToken);
        var ceremony = await coreDb.CeremonyRequests
            .FromSqlInterpolated($@"SELECT * FROM core.ceremony_requests WHERE ""Id"" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (ceremony is null || ceremony.AdmissionCaseId != admissionCase.Id)
            return Results.Conflict(new { message = "La solicitud de ceremonia cambió durante la operación." });

        if (ceremony.Status == CeremonyCodes.RequestStatus.Completed)
        {
            await coreTransaction.CommitAsync(cancellationToken);
            var originalActor = await FindAuditActorAsync(
                coreDb,
                "admission.ceremony.completed",
                nameof(CeremonyRequest),
                ceremony.Id,
                cancellationToken) ?? GetSubject(httpContext.User);
            await ReconcileCompletionAsync(
                admissionCase,
                ceremony,
                request.CeremonyDate,
                meeting.Id,
                record.ExtractDocumentVersionId.Value,
                authorization.Id,
                originalActor,
                admissionsDb,
                cancellationToken);

            return Results.Ok(new
            {
                ceremony.Id,
                ceremony.Status,
                ceremony.MemberId,
                admissionCaseId = admissionCase.Id,
                admissionStatus = admissionCase.Status,
                meetingId = meeting.Id,
                alreadyCompleted = true
            });
        }

        if (ceremony.Status != CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia debe estar institucionalmente autorizada antes de registrar su realización." });

        admissionsDb.ChangeTracker.Clear();
        admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .Include(x => x.CommissionAppointments)
            .SingleOrDefaultAsync(x => x.Id == ceremony.AdmissionCaseId.Value, cancellationToken);
        if (admissionCase is null)
            return Results.Conflict(new { message = "El expediente de admisión ya no está disponible." });

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);
        if (!projection.Decision.CanProceed)
            return Results.Conflict(new { message = "El expediente dejó de estar habilitado y no puede materializar la admisión.", eligibility = projection.Decision });

        var evidenceReference = $"plancha:{authorization.DocumentCode}; authorization:{authorization.Id}; extract:{record.ExtractDocumentVersionId.Value}; meeting:{meeting.Id}; admission:{admissionCase.Id}";
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

            var targetMembership = new PMGM.Api.Modules.Membership.Entities.Membership
            {
                MemberId = memberId,
                OrganizationId = admissionCase.OrganizationId,
                MembershipType = "regular",
                StartDate = request.CeremonyDate,
                Status = MembershipCodes.MembershipStatus.Active,
                EvidenceReference = evidenceReference
            };

            if (admissionCase.AffiliationProcedure == AdmissionCodes.AffiliationProcedure.Transfer)
            {
                if (admissionCase.OriginOrganizationId is null)
                    return Results.Conflict(new { message = "La afiliación con traslado no identifica el Taller de origen." });
                if (admissionCase.OriginOrganizationId.Value == admissionCase.OrganizationId)
                    return Results.Conflict(new { message = "El Taller de origen y el Taller de destino de un traslado deben ser distintos." });

                var sourceMemberships = await coreDb.Memberships
                    .Where(x => x.MemberId == memberId &&
                                x.OrganizationId == admissionCase.OriginOrganizationId.Value &&
                                x.Status == MembershipCodes.MembershipStatus.Active &&
                                x.EndDate == null)
                    .ToListAsync(cancellationToken);
                if (sourceMemberships.Count != 1)
                    return Results.Conflict(new { message = "El traslado requiere exactamente una pertenencia activa en el Taller de origen indicado." });

                var sourceMembership = sourceMemberships[0];
                if (sourceMembership.StartDate is not null && request.CeremonyDate <= sourceMembership.StartDate.Value)
                    return Results.BadRequest(new { message = "La fecha efectiva del traslado debe ser posterior al inicio de la pertenencia de origen." });

                sourceMembership.EndDate = request.CeremonyDate.AddDays(-1);
                sourceMembership.Status = MembershipCodes.MembershipStatus.Transferred;
                sourceMembership.EndReason = "Afiliación con traslado a otro Taller";

                var transfer = new MemberTransfer
                {
                    MemberId = memberId,
                    SourceMembershipId = sourceMembership.Id,
                    SourceOrganizationId = sourceMembership.OrganizationId,
                    TargetOrganizationId = admissionCase.OrganizationId,
                    TargetMembership = targetMembership,
                    RequestedDate = request.CeremonyDate,
                    ProposedEffectiveDate = request.CeremonyDate,
                    ApprovedEffectiveDate = request.CeremonyDate,
                    Status = MembershipCodes.TransferStatus.Executed,
                    Reason = "Afiliación con traslado aprobada por el procedimiento institucional.",
                    Resolution = "Ejecutada al materializar la ceremonia de Afiliación.",
                    EvidenceReference = evidenceReference,
                    ExecutedAtUtc = DateTimeOffset.UtcNow
                };

                coreDb.MemberTransfers.Add(transfer);
                coreDb.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent
                {
                    MemberId = memberId,
                    OrganizationId = admissionCase.OrganizationId,
                    EventType = MembershipCodes.InstitutionalStatus.WorkshopTransfer,
                    EffectiveDate = request.CeremonyDate,
                    EvidenceReference = evidenceReference,
                    Reason = transfer.Resolution,
                    Notes = $"Transferencia desde {sourceMembership.OrganizationId} hacia {admissionCase.OrganizationId}."
                });

                audit.Add(
                    httpContext,
                    "membership.transfer.executed",
                    nameof(MemberTransfer),
                    transfer.Id.ToString(),
                    sourceMembership.OrganizationId,
                    AuditResults.Success,
                    new
                    {
                        transfer.MemberId,
                        transfer.SourceOrganizationId,
                        transfer.TargetOrganizationId,
                        effectiveDate = request.CeremonyDate,
                        sourceMembershipId = sourceMembership.Id,
                        targetMembershipId = targetMembership.Id,
                        admissionCaseId = admissionCase.Id
                    });
            }
            else
            {
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
            }

            coreDb.Memberships.Add(targetMembership);
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
        var actorSubject = GetSubject(httpContext.User);
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
                meetingId = meeting.Id,
                authorizationDocumentId = authorization.Id,
                extractDocumentVersionId = record.ExtractDocumentVersionId.Value,
                admissionCaseId = admissionCase.Id
            });

        await coreDb.SaveChangesAsync(cancellationToken);
        await coreTransaction.CommitAsync(cancellationToken);

        await ReconcileCompletionAsync(
            admissionCase,
            ceremony,
            request.CeremonyDate,
            meeting.Id,
            record.ExtractDocumentVersionId.Value,
            authorization.Id,
            actorSubject,
            admissionsDb,
            cancellationToken);

        return Results.Ok(new
        {
            ceremony.Id,
            ceremony.Status,
            memberId,
            admissionCaseId = admissionCase.Id,
            admissionStatus = admissionCase.Status,
            effectiveDate = request.CeremonyDate,
            meetingId = meeting.Id,
            authorizationDocumentId = authorization.Id,
            extractDocumentVersionId = record.ExtractDocumentVersionId.Value,
            alreadyCompleted = false
        });
    }

    private static async Task ReconcileCompletionAsync(
        AdmissionCase admissionCase,
        CeremonyRequest ceremony,
        DateOnly ceremonyDate,
        Guid meetingId,
        Guid extractDocumentVersionId,
        Guid authorizationDocumentId,
        string recordedBySubject,
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
                AsOfDate = ceremonyDate,
                SourceReference = reference,
                Notes = "Tenida ceremonial cerrada con Plancha de Gran Secretaría y Extracto de Acta adjuntos.",
                StructuredDataJson = JsonSerializer.Serialize(new
                {
                    ceremonyRequestId = ceremony.Id,
                    ceremony.CeremonyType,
                    ceremony.MemberId,
                    ceremonyDate,
                    meetingId,
                    extractDocumentVersionId,
                    authorizationDocumentId
                }),
                RecordedBySubject = recordedBySubject
            });
        }

        await admissionsDb.SaveChangesAsync(cancellationToken);
    }

    private static async Task ReconcileCaseLinkAsync(
        AdmissionCase admissionCase,
        Guid ceremonyRequestId,
        string recordedBySubject,
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
                RecordedBySubject = recordedBySubject
            });
        }

        await admissionsDb.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string?> FindAuditActorAsync(
        PmgmDbContext coreDb,
        string action,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
        => await coreDb.AuditEvents.AsNoTracking()
            .Where(x => x.Action == action &&
                        x.EntityType == entityType &&
                        x.EntityId == entityId.ToString() &&
                        x.Result == AuditResults.Success)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => x.ActorSubject)
            .FirstOrDefaultAsync(cancellationToken);

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

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
    Guid MeetingId,
    DateOnly CeremonyDate);
