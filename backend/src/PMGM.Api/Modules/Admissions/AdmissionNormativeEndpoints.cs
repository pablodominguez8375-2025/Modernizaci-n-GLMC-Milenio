using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using MembershipEntity = PMGM.Api.Modules.Membership.Entities.Membership;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

/// <summary>
/// Residual controls from historical PR #116, adapted to the current dev model.
/// Decisions are recorded in the existing admission_decisions ledger; no second
/// admissions model or unapproved tariff/authority rule is introduced here.
/// </summary>
public static class AdmissionNormativeEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionNormativeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admisiones")
            .WithTags("Afiliaciones e incorporaciones - controles reglamentarios")
            .RequireAuthorization();

        group.MapPost("/expedientes/{caseId:guid}/revision-articulo-2-3", RecordArticle23ReviewAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/indulto-gran-maestria", RecordGrandMasterPardonAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/reconocimiento-regularidad", RecordGrandMasterRegularityRecognitionAsync);
        group.MapPost("/expedientes/{caseId:guid}/comision-informacion", AppointInformationCommissionAsync);
        group.MapPost("/expedientes/{caseId:guid}/comision-informacion/conclusion", CompleteInformationCommissionAsync);
        group.MapPost("/expedientes/{caseId:guid}/materializar", MaterializeMembershipAsync);
        return endpoints;
    }

    private static async Task<IResult> MaterializeMembershipAsync(Guid caseId, MaterializeAdmissionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.Evidence).Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(context.User, admissionCase.OrganizationId)) return Results.Forbid();
        if (admissionCase.MemberId is null)
            return Results.BadRequest(new { message = "El expediente debe estar vinculado a un hermano antes de materializarse." });
        if (request.EffectiveDate > ChileToday())
            return Results.BadRequest(new { message = "La fecha efectiva no puede ser futura." });
        if (request.EffectiveDate < DateOnly.FromDateTime(admissionCase.CreatedAtUtc.Date))
            return Results.BadRequest(new { message = "La fecha efectiva no puede ser anterior a la creación del expediente." });
        if (string.IsNullOrWhiteSpace(request.EvidenceReference))
            return Results.BadRequest(new { message = "Debe indicar la resolución o evidencia que autoriza la materialización." });

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);
        if (!projection.Decision.CanProceed)
            return Results.Conflict(new { message = "El expediente aún no cumple los requisitos normativos.", projection.Decision.Status, projection.Decision.Requirements });

        await using var transaction = await coreDb.Database.BeginTransactionAsync(ct);
        var existing = await coreDb.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == admissionCase.MemberId.Value && x.OrganizationId == admissionCase.OrganizationId &&
                        x.Status == MembershipCodes.MembershipStatus.Active && x.StartDate == request.EffectiveDate)
            .Select(x => new { x.Id, x.MemberId, x.OrganizationId, x.StartDate, x.Status })
            .SingleOrDefaultAsync(ct);
        if (existing is not null)
        {
            await transaction.CommitAsync(ct);
            return Results.Ok(new { idempotent = true, membership = existing });
        }

        var membership = new MembershipEntity
        {
            MemberId = admissionCase.MemberId.Value,
            OrganizationId = admissionCase.OrganizationId,
            MembershipType = admissionCase.AdmissionType,
            StartDate = request.EffectiveDate,
            Status = MembershipCodes.MembershipStatus.Active,
            EvidenceReference = request.EvidenceReference.Trim()
        };
        coreDb.Memberships.Add(membership);
        coreDb.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent
        {
            MemberId = membership.MemberId,
            OrganizationId = membership.OrganizationId,
            EventType = MembershipCodes.InstitutionalStatus.Active,
            EffectiveDate = request.EffectiveDate,
            EvidenceReference = membership.EvidenceReference,
            Reason = $"Materialización de expediente de {admissionCase.AdmissionType}."
        });
        audit.Add(context, "admission.membership.materialized", nameof(AdmissionCase), caseId.ToString(), admissionCase.OrganizationId,
            AuditResults.Success, new { caseId, membershipId = membership.Id, request.EffectiveDate });
        await coreDb.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        admissionCase.Status = AdmissionWorkflowCodes.CaseStatus.Resolved;
        admissionsDb.AdmissionDecisions.Add(NewDecision(caseId, AdmissionWorkflowCodes.DecisionType.MembershipMaterialized,
            CeremonyCodes.ValidationStatus.Approved, request.EffectiveDate, request.EvidenceReference,
            $"membershipId={membership.Id:D}", Subject(context.User)));
        await admissionsDb.SaveChangesAsync(ct);
        return Results.Ok(new { idempotent = false, membershipId = membership.Id, admissionCaseId = caseId, membership.StartDate, membership.Status });
    }

    private static Task<IResult> RecordGrandMasterPardonAsync(Guid caseId, AdmissionAuthorityDecisionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
        => RecordAuthorityDecisionAsync(caseId, AdmissionWorkflowCodes.DecisionType.GrandMasterPardon,
            "admission.grand_master.pardon.recorded", request, context, admissionsDb, coreDb, access, audit, ct,
            requireIncorporation: false);

    private static Task<IResult> RecordGrandMasterRegularityRecognitionAsync(Guid caseId, AdmissionAuthorityDecisionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
        => RecordAuthorityDecisionAsync(caseId, AdmissionWorkflowCodes.DecisionType.GrandMasterRegularityRecognition,
            "admission.grand_master.regularity_recognition.recorded", request, context, admissionsDb, coreDb, access, audit, ct,
            requireIncorporation: true);

    private static async Task<IResult> RecordArticle23ReviewAsync(Guid caseId, Article23ReviewRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanValidateCeremonyInternalAffairs(context.User)) return Results.Forbid();
        if (request.AsOfDate > ChileToday()) return Results.BadRequest(new { message = "La revisión no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe indicar la fuente que respalda la revisión del art. 2.3." });
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved) return Results.Conflict(new { message = "El expediente ya está resuelto." });
        var blocked = request.HasRayamiento || request.HasTribunalForcedWithdrawal;
        var decision = NewDecision(caseId, AdmissionWorkflowCodes.DecisionType.Article23Review,
            blocked ? CeremonyCodes.ValidationStatus.Rejected : CeremonyCodes.ValidationStatus.Approved,
            request.AsOfDate, request.SourceReference, JsonSerializer.Serialize(new { request.HasRayamiento, request.HasTribunalForcedWithdrawal, request.Notes }),
            Subject(context.User));
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, "admission.article_2_3.reviewed", nameof(AdmissionDecision), decision.Id.ToString(), admissionCase.OrganizationId,
            blocked ? AuditResults.Rejected : AuditResults.Success, new { request.HasRayamiento, request.HasTribunalForcedWithdrawal, decision.Status });
        await coreDb.SaveChangesAsync(ct);
        return Results.Ok(ToDto(decision));
    }

    private static async Task<IResult> RecordAuthorityDecisionAsync(Guid caseId, string decisionType, string auditAction,
        AdmissionAuthorityDecisionRequest request, HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct, bool requireIncorporation)
    {
        if (!access.CanProvideGrandMasterApproval(context.User)) return Results.Forbid();
        if (request.AsOfDate > ChileToday()) return Results.BadRequest(new { message = "La decisión no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe registrar la resolución o fuente institucional." });
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (requireIncorporation && admissionCase.AdmissionType != CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "El reconocimiento de regularidad sólo aplica a incorporaciones." });
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved) return Results.Conflict(new { message = "El expediente ya está resuelto." });
        var decision = NewDecision(caseId, decisionType,
            request.Approved ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Rejected,
            request.AsOfDate, request.SourceReference, request.Notes, Subject(context.User));
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, auditAction, nameof(AdmissionDecision), decision.Id.ToString(), admissionCase.OrganizationId,
            request.Approved ? AuditResults.Success : AuditResults.Rejected, new { decision.DecisionType, decision.Status });
        await coreDb.SaveChangesAsync(ct);
        return Results.Ok(ToDto(decision));
    }

    private static async Task<IResult> AppointInformationCommissionAsync(Guid caseId, AppointAdmissionCommissionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(context.User, admissionCase.OrganizationId)) return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved) return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.AppointmentDate > ChileToday()) return Results.BadRequest(new { message = "El nombramiento no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe indicar el acta o fuente del nombramiento." });
        var memberIds = request.MemberIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
        if (memberIds.Length != 3) return Results.BadRequest(new { message = "La comisión debe estar integrada exactamente por tres Maestros distintos." });

        var validMembers = await coreDb.Memberships.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) &&
                        x.OrganizationId == admissionCase.OrganizationId &&
                        x.Status == MembershipCodes.MembershipStatus.Active &&
                        x.StartDate <= request.AppointmentDate &&
                        (x.EndDate == null || x.EndDate >= request.AppointmentDate) &&
                        x.Member.CurrentDegree == "master")
            .Select(x => x.MemberId).Distinct().ToListAsync(ct);
        if (validMembers.Count != 3)
            return Results.BadRequest(new { message = "Los tres integrantes deben ser Maestros con pertenencia activa al Taller en la fecha del nombramiento." });

        var decision = NewDecision(caseId, AdmissionWorkflowCodes.DecisionType.InformationCommissionAppointed,
            CeremonyCodes.ValidationStatus.Approved, request.AppointmentDate, request.SourceReference,
            JsonSerializer.Serialize(new { memberIds }), Subject(context.User));
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, "admission.information_commission.appointed", nameof(AdmissionDecision), decision.Id.ToString(),
            admissionCase.OrganizationId, AuditResults.Success, new { memberIds, request.AppointmentDate });
        await coreDb.SaveChangesAsync(ct);
        return Results.Created($"/api/admisiones/expedientes/{caseId}/comision-informacion/{decision.Id}", ToDto(decision));
    }

    private static async Task<IResult> CompleteInformationCommissionAsync(Guid caseId, CompleteAdmissionCommissionRequest request,
        HttpContext context, AdmissionsDbContext admissionsDb, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(context.User, admissionCase.OrganizationId)) return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved) return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.AsOfDate > ChileToday()) return Results.BadRequest(new { message = "La conclusión no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe indicar el informe o acta de la comisión." });
        var appointment = await admissionsDb.AdmissionDecisions.AsNoTracking()
            .Where(x => x.AdmissionCaseId == caseId && x.DecisionType == AdmissionWorkflowCodes.DecisionType.InformationCommissionAppointed)
            .OrderByDescending(x => x.RecordedAtUtc).FirstOrDefaultAsync(ct);
        if (appointment is null) return Results.Conflict(new { message = "Debe existir un nombramiento vigente de comisión antes de registrar su conclusión." });
        if (request.AsOfDate < appointment.AsOfDate) return Results.BadRequest(new { message = "La conclusión no puede ser anterior al nombramiento vigente." });

        var decision = NewDecision(caseId, AdmissionWorkflowCodes.DecisionType.InformationCommissionCompleted,
            request.Completed ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Rejected,
            request.AsOfDate, request.SourceReference, request.Notes, Subject(context.User));
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, "admission.information_commission.completed", nameof(AdmissionDecision), decision.Id.ToString(),
            admissionCase.OrganizationId, request.Completed ? AuditResults.Success : AuditResults.Rejected,
            new { request.Completed, appointmentDecisionId = appointment.Id });
        await coreDb.SaveChangesAsync(ct);
        return Results.Ok(ToDto(decision));
    }

    private static AdmissionDecision NewDecision(Guid caseId, string type, string status, DateOnly asOfDate, string source, string? notes, string subject)
        => new() { AdmissionCaseId = caseId, DecisionType = type, Status = status, AsOfDate = asOfDate,
            SourceReference = source.Trim(), Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), RecordedBySubject = subject };

    private static object ToDto(AdmissionDecision decision) => new { decision.Id, decision.AdmissionCaseId, decision.DecisionType,
        decision.Status, decision.AsOfDate, decision.SourceReference, decision.Notes, decision.RecordedBySubject, decision.RecordedAtUtc };

    private static string Subject(ClaimsPrincipal user) => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private static DateOnly ChileToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
}

public sealed record Article23ReviewRequest(bool HasRayamiento, bool HasTribunalForcedWithdrawal, DateOnly AsOfDate, string SourceReference, string? Notes);
public sealed record AdmissionAuthorityDecisionRequest(bool Approved, DateOnly AsOfDate, string SourceReference, string? Notes);
public sealed record AppointAdmissionCommissionRequest(IReadOnlyCollection<Guid>? MemberIds, DateOnly AppointmentDate, string SourceReference);
public sealed record CompleteAdmissionCommissionRequest(bool Completed, DateOnly AsOfDate, string SourceReference, string? Notes);
public sealed record MaterializeAdmissionRequest(DateOnly EffectiveDate, string EvidenceReference);
