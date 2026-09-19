using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Admissions;

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

        return endpoints;
    }

    private static async Task<IResult> RecordArticle23ReviewAsync(
        Guid caseId,
        Article23ReviewRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        if (!access.CanValidateCeremonyInternalAffairs(context.User)) return Results.Forbid();
        if (request.AsOfDate > ChileToday()) return Results.BadRequest(new { message = "La revisión no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe indicar la fuente que respalda la revisión del art. 2.3." });

        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved) return Results.Conflict(new { message = "El expediente ya está resuelto." });

        var blocked = request.HasRayamiento || request.HasTribunalForcedWithdrawal;
        var decision = NewDecision(
            caseId,
            AdmissionWorkflowCodes.DecisionType.Article23Review,
            blocked ? CeremonyCodes.ValidationStatus.Rejected : CeremonyCodes.ValidationStatus.Approved,
            request.AsOfDate,
            request.SourceReference,
            request.Notes,
            GetSubject(context.User),
            new { request.HasRayamiento, request.HasTribunalForcedWithdrawal });

        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, "admission.article_2_3.reviewed", nameof(AdmissionDecision), decision.Id.ToString(), admissionCase.OrganizationId,
            blocked ? AuditResults.Rejected : AuditResults.Success,
            new { request.HasRayamiento, request.HasTribunalForcedWithdrawal, decision.Status });
        await coreDb.SaveChangesAsync(ct);

        return Results.Ok(ToDecisionDto(decision));
    }

    private static async Task<IResult> RecordGrandMasterPardonAsync(
        Guid caseId,
        AdmissionAuthorityDecisionRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        if (!access.CanProvideGrandMasterApproval(context.User)) return Results.Forbid();
        return await RecordAuthorityDecisionAsync(
            caseId,
            AdmissionWorkflowCodes.DecisionType.GrandMasterPardon,
            "admission.grand_master.pardon.recorded",
            request,
            context,
            admissionsDb,
            coreDb,
            audit,
            ct);
    }

    private static async Task<IResult> RecordGrandMasterRegularityRecognitionAsync(
        Guid caseId,
        AdmissionAuthorityDecisionRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        if (!access.CanProvideGrandMasterApproval(context.User)) return Results.Forbid();

        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (admissionCase.AdmissionType != CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "El reconocimiento de regularidad de Obediencia sólo aplica a Incorporación." });
        if (admissionCase.OriginObedienceRecognizedAsRegular != false)
            return Results.Conflict(new { message = "Este expediente no registra una Obediencia de origen no reconocida como regular." });

        return await RecordAuthorityDecisionAsync(
            caseId,
            AdmissionWorkflowCodes.DecisionType.GrandMasterRegularityRecognition,
            "admission.grand_master.regularity_recognition.recorded",
            request,
            context,
            admissionsDb,
            coreDb,
            audit,
            ct);
    }

    private static async Task<IResult> RecordAuthorityDecisionAsync(
        Guid caseId,
        string decisionType,
        string auditAction,
        AdmissionAuthorityDecisionRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IAuditService audit,
        CancellationToken ct)
    {
        if (request.AsOfDate > ChileToday()) return Results.BadRequest(new { message = "La decisión no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe registrar la resolución o fuente institucional." });

        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved) return Results.Conflict(new { message = "El expediente ya está resuelto." });

        var decision = NewDecision(
            caseId,
            decisionType,
            request.Approved ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Rejected,
            request.AsOfDate,
            request.SourceReference,
            request.Notes,
            GetSubject(context.User),
            new { request.Approved });

        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, auditAction, nameof(AdmissionDecision), decision.Id.ToString(), admissionCase.OrganizationId,
            request.Approved ? AuditResults.Success : AuditResults.Rejected,
            new { decision.DecisionType, decision.Status });
        await coreDb.SaveChangesAsync(ct);
        return Results.Ok(ToDecisionDto(decision));
    }

    private static async Task<IResult> AppointInformationCommissionAsync(
        Guid caseId,
        AppointAdmissionCommissionRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanAppointAdmissionCommission(context.User, admissionCase.OrganizationId)) return Results.Forbid();
        if (admissionCase.AdmissionType != CeremonyCodes.Type.Incorporation)
            return Results.BadRequest(new { message = "La comisión de información de este incremento aplica a Incorporación desde otra Obediencia." });
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.AppointmentDate > ChileToday())
            return Results.BadRequest(new { message = "El nombramiento no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "Debe indicar el acta o fuente del nombramiento." });

        var memberIds = request.MemberIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
        if (memberIds.Length != 3)
            return Results.BadRequest(new { message = "El art. 2.5 exige una comisión integrada exactamente por tres Maestros distintos." });

        var validMembers = await coreDb.Memberships.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) &&
                        x.OrganizationId == admissionCase.OrganizationId &&
                        x.Status == MembershipCodes.MembershipStatus.Active &&
                        x.StartDate <= request.AppointmentDate &&
                        (x.EndDate == null || x.EndDate >= request.AppointmentDate) &&
                        x.Member.CurrentDegree == "master")
            .Select(x => x.MemberId)
            .Distinct()
            .ToListAsync(ct);

        if (validMembers.Count != 3)
            return Results.BadRequest(new { message = "Los tres integrantes deben ser Maestros con pertenencia activa al Taller en la fecha del nombramiento." });

        var groupId = Guid.NewGuid();
        var subject = GetSubject(context.User);
        var appointments = memberIds.Select(memberId => new AdmissionCommissionAppointment
        {
            AdmissionCaseId = caseId,
            AppointmentGroupId = groupId,
            MemberId = memberId,
            AppointmentDate = request.AppointmentDate,
            SourceReference = request.SourceReference.Trim(),
            AppointedBySubject = subject
        }).ToList();

        admissionsDb.AdmissionCommissionAppointments.AddRange(appointments);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, "admission.information_commission.appointed", nameof(AdmissionCommissionAppointment), groupId.ToString(),
            admissionCase.OrganizationId, AuditResults.Success,
            new { appointmentGroupId = groupId, memberCount = 3, request.AppointmentDate });
        await coreDb.SaveChangesAsync(ct);

        return Results.Created($"/api/admisiones/expedientes/{caseId}/comision-informacion/{groupId}", new
        {
            appointmentGroupId = groupId,
            request.AppointmentDate,
            request.SourceReference,
            memberIds
        });
    }

    private static async Task<IResult> CompleteInformationCommissionAsync(
        Guid caseId,
        CompleteAdmissionCommissionRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.CommissionAppointments)
            .SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageOrganization(context.User, admissionCase.OrganizationId) &&
            !access.CanAppointAdmissionCommission(context.User, admissionCase.OrganizationId))
            return Results.Forbid();
        if (request.AsOfDate > ChileToday()) return Results.BadRequest(new { message = "La conclusión no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference)) return Results.BadRequest(new { message = "Debe indicar la referencia del informe/acta de la comisión." });

        var latestGroup = admissionCase.CommissionAppointments
            .GroupBy(x => x.AppointmentGroupId)
            .OrderByDescending(x => x.Max(y => y.RecordedAtUtc))
            .FirstOrDefault();
        if (latestGroup is null || latestGroup.Select(x => x.MemberId).Distinct().Count() != 3)
            return Results.Conflict(new { message = "Debe existir una comisión vigente de tres Maestros antes de registrar su conclusión." });

        var decision = NewDecision(
            caseId,
            AdmissionWorkflowCodes.DecisionType.InformationCommissionCompleted,
            request.Completed ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Rejected,
            request.AsOfDate,
            request.SourceReference,
            request.Notes,
            GetSubject(context.User),
            new { request.Completed, appointmentGroupId = latestGroup.Key });

        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, "admission.information_commission.completed", nameof(AdmissionDecision), decision.Id.ToString(),
            admissionCase.OrganizationId, request.Completed ? AuditResults.Success : AuditResults.Rejected,
            new { request.Completed, appointmentGroupId = latestGroup.Key });
        await coreDb.SaveChangesAsync(ct);

        return Results.Ok(ToDecisionDto(decision));
    }

    private static AdmissionDecision NewDecision(
        Guid caseId,
        string type,
        string status,
        DateOnly date,
        string sourceReference,
        string? notes,
        string subject,
        object structuredData)
        => new()
        {
            AdmissionCaseId = caseId,
            DecisionType = type,
            Status = status,
            AsOfDate = date,
            SourceReference = sourceReference.Trim(),
            Notes = Normalize(notes),
            StructuredDataJson = JsonSerializer.Serialize(structuredData),
            RecordedBySubject = subject
        };

    private static object ToDecisionDto(AdmissionDecision decision) => new
    {
        decision.Id,
        decision.AdmissionCaseId,
        decision.DecisionType,
        decision.Status,
        decision.AsOfDate,
        decision.SourceReference,
        decision.Notes,
        decision.StructuredDataJson,
        decision.RecordedAtUtc
    };

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly ChileToday()
    {
        var chile = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chile.DateTime);
    }
}

public sealed record Article23ReviewRequest(
    bool HasRayamiento,
    bool HasTribunalForcedWithdrawal,
    DateOnly AsOfDate,
    string SourceReference,
    string? Notes);

public sealed record AdmissionAuthorityDecisionRequest(
    bool Approved,
    DateOnly AsOfDate,
    string SourceReference,
    string? Notes);

public sealed record AppointAdmissionCommissionRequest(
    IReadOnlyCollection<Guid>? MemberIds,
    DateOnly AppointmentDate,
    string SourceReference);

public sealed record CompleteAdmissionCommissionRequest(
    bool Completed,
    DateOnly AsOfDate,
    string SourceReference,
    string? Notes);
