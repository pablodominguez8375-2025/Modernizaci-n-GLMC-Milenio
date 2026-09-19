using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.Admissions;

public static class AdmissionLodgeDecisionEndpoints
{
    public static IEndpointRouteBuilder MapAdmissionLodgeDecisionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admisiones")
            .WithTags("Afiliaciones e incorporaciones - decisiones del Taller")
            .RequireAuthorization();

        group.MapPost("/expedientes/{caseId:guid}/presentacion-primer-grado", RecordFirstDegreePresentationAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/tercer-grado", RecordThirdDegreeDecisionAsync);
        group.MapPost("/expedientes/{caseId:guid}/decisiones/balotaje-primer-grado", RecordFirstDegreeBallotAsync);
        group.MapGet("/expedientes/{caseId:guid}/habilitacion-procedimiento", GetProcedureEligibilityAsync);

        return endpoints;
    }

    private static async Task<IResult> RecordFirstDegreePresentationAsync(
        Guid caseId,
        AdmissionFirstDegreePresentationRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(context.User, admissionCase.OrganizationId)) return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.PresentationDate > ChileToday())
            return Results.BadRequest(new { message = "La presentación no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "Debe indicar el acta que acredita la lectura en Cámara de Primer Grado." });

        var article23 = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.Article23Review);
        var pardon = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.GrandMasterPardon);
        if (article23 is null)
            return Results.Conflict(new { message = "Régimen Interior debe completar primero la revisión del art. 2.3." });
        var validPardon = pardon?.Status == CeremonyCodes.ValidationStatus.Approved &&
                          pardon.RecordedAtUtc >= article23.RecordedAtUtc;
        if (article23.Status == CeremonyCodes.ValidationStatus.Rejected && !validPardon)
            return Results.Conflict(new { message = "El expediente mantiene un impedimento del art. 2.3 sin indulto habilitante posterior a la revisión." });

        var decision = NewDecision(
            caseId,
            AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreePresentation,
            CeremonyCodes.ValidationStatus.Approved,
            request.PresentationDate,
            request.SourceReference,
            request.Notes,
            GetSubject(context.User),
            new { chamber = "first_degree", readToLodge = true });

        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);
        audit.Add(context, "admission.lodge.first_degree_presentation.recorded", nameof(AdmissionDecision), decision.Id.ToString(),
            admissionCase.OrganizationId, AuditResults.Success,
            new { request.PresentationDate, decision.Status });
        await coreDb.SaveChangesAsync(ct);

        return Results.Ok(ToDecisionDto(decision));
    }

    private static async Task<IResult> RecordThirdDegreeDecisionAsync(
        Guid caseId,
        AdmissionThirdDegreeVoteRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.Decisions)
            .Include(x => x.CommissionAppointments)
            .SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(context.User, admissionCase.OrganizationId)) return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.AsOfDate > ChileToday())
            return Results.BadRequest(new { message = "La votación no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "Debe registrar la referencia del acta de 3.er grado." });

        var article23 = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.Article23Review);
        var pardon = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.GrandMasterPardon);
        var article23Enabled = article23?.Status == CeremonyCodes.ValidationStatus.Approved ||
                               (article23?.Status == CeremonyCodes.ValidationStatus.Rejected &&
                                pardon?.Status == CeremonyCodes.ValidationStatus.Approved &&
                                pardon.RecordedAtUtc >= article23.RecordedAtUtc);
        if (!article23Enabled)
            return Results.Conflict(new { message = "El expediente no tiene un control habilitante vigente del art. 2.3." });

        var presentation = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreePresentation);
        if (presentation?.Status != CeremonyCodes.ValidationStatus.Approved ||
            presentation.RecordedAtUtc < article23!.RecordedAtUtc)
            return Results.Conflict(new { message = "La solicitud debe presentarse nuevamente en 1.er grado después del control vigente del art. 2.3." });
        if (request.AsOfDate < presentation.AsOfDate)
            return Results.BadRequest(new { message = "La votación de 3.er grado no puede ser anterior a la presentación en 1.er grado." });

        if (AdmissionProcedureRules.RequiresInformationCommission(admissionCase.AdmissionType, admissionCase.AffiliationProcedure))
        {
            var waiver = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.InformationCommissionWaiver);
            var waiverValid = waiver?.Status == CeremonyCodes.ValidationStatus.Approved &&
                              AdmissionProcedureRules.AllowsInformationCommissionWaiver(admissionCase.AdmissionType, admissionCase.AffiliationProcedure) &&
                              waiver.AsOfDate <= request.AsOfDate;
            if (!waiverValid)
            {
                var commissionGroup = admissionCase.CommissionAppointments
                    .GroupBy(x => x.AppointmentGroupId)
                    .OrderByDescending(x => x.Max(y => y.RecordedAtUtc))
                    .FirstOrDefault();
                var commissionCompleted = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.InformationCommissionCompleted);
                if (commissionGroup is null ||
                    commissionGroup.Select(x => x.MemberId).Distinct().Count() != 3 ||
                    commissionCompleted?.Status != CeremonyCodes.ValidationStatus.Approved ||
                    !DecisionReferencesAppointmentGroup(commissionCompleted, commissionGroup.Key) ||
                    commissionCompleted.AsOfDate < commissionGroup.Max(x => x.AppointmentDate) ||
                    commissionCompleted.AsOfDate > request.AsOfDate)
                    return Results.Conflict(new { message = "El art. 2.5 exige que la comisión vigente de tres Maestros esté concluida antes de la decisión de 3.er grado; sólo el traslado puede omitirla mediante dispensa registrada de Cámara del Medio." });
            }
        }

        var result = AdmissionEligibilityPolicy.EvaluateThirdDegreeVote(
            request.PresentMasters,
            request.VotesInFavor,
            request.VotesAgainst,
            request.Abstentions);
        if (!result.CanProceed && !result.IsRejected)
            return Results.BadRequest(new { message = result.Reason, result.Code });

        var status = result.CanProceed ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Rejected;
        var decision = NewDecision(
            caseId,
            AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval,
            status,
            request.AsOfDate,
            request.SourceReference,
            request.Notes,
            GetSubject(context.User),
            new
            {
                request.PresentMasters,
                request.VotesInFavor,
                request.VotesAgainst,
                request.Abstentions,
                threshold = "two_thirds_of_present_masters",
                result.Code
            });

        admissionCase.Status = result.IsRejected ? AdmissionWorkflowCodes.CaseStatus.Rejected : AdmissionWorkflowCodes.CaseStatus.UnderReview;
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);

        audit.Add(context, "admission.lodge.third_degree_decision.recorded", nameof(AdmissionDecision), decision.Id.ToString(),
            admissionCase.OrganizationId, result.IsRejected ? AuditResults.Rejected : AuditResults.Success,
            new { request.PresentMasters, request.VotesInFavor, request.VotesAgainst, request.Abstentions, decision.Status, result.Code });
        await coreDb.SaveChangesAsync(ct);

        return Results.Ok(new { decision = ToDecisionDto(decision), result.Code, result.Reason });
    }

    private static async Task<IResult> RecordFirstDegreeBallotAsync(
        Guid caseId,
        AdmissionFirstDegreeBallotRequest request,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases
            .Include(x => x.Decisions)
            .SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(context.User, admissionCase.OrganizationId)) return Results.Forbid();
        if (admissionCase.Status == AdmissionWorkflowCodes.CaseStatus.Resolved)
            return Results.Conflict(new { message = "El expediente ya está resuelto." });
        if (request.AsOfDate > ChileToday())
            return Results.BadRequest(new { message = "El balotaje no puede registrarse con fecha futura." });
        if (string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "Debe registrar la referencia del acta del balotaje." });

        var thirdDegree = LatestDecision(admissionCase, AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval);
        if (thirdDegree?.Status != CeremonyCodes.ValidationStatus.Approved)
            return Results.Conflict(new { message = "La tramitación debe estar aprobada en 3.er grado antes del balotaje." });
        if (request.AsOfDate <= thirdDegree.AsOfDate)
            return Results.BadRequest(new { message = "El art. 2.4 exige que el balotaje se efectúe en fecha posterior a la decisión de 3.er grado." });

        var rounds = request.Ballots?.Select(x =>
            new CandidateBallotRound(x.ProcedureNumber, x.EligibleVoters, x.WhiteBallots, x.BlackBallots)).ToArray()
            ?? Array.Empty<CandidateBallotRound>();
        var result = AdmissionEligibilityPolicy.EvaluateFirstDegreeBallot(rounds, request.BallotApproved);
        if (!result.CanProceed && !result.IsRejected)
            return Results.BadRequest(new { message = result.Reason, result.Code });

        var status = result.CanProceed ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Rejected;
        var decision = NewDecision(
            caseId,
            AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreeBallot,
            status,
            request.AsOfDate,
            request.SourceReference,
            request.Notes,
            GetSubject(context.User),
            new
            {
                procedure = "same_as_initiation",
                ballots = rounds.Select(x => new { x.ProcedureNumber, x.EligibleVoters, x.WhiteBallots, x.BlackBallots }),
                request.BallotApproved,
                result.Code
            });

        admissionCase.Status = result.IsRejected ? AdmissionWorkflowCodes.CaseStatus.Rejected : AdmissionWorkflowCodes.CaseStatus.UnderReview;
        admissionsDb.AdmissionDecisions.Add(decision);
        await admissionsDb.SaveChangesAsync(ct);

        audit.Add(context, "admission.lodge.first_degree_ballot.recorded", nameof(AdmissionDecision), decision.Id.ToString(),
            admissionCase.OrganizationId, result.IsRejected ? AuditResults.Rejected : AuditResults.Success,
            new { ballots = rounds, decision.Status, result.Code });
        await coreDb.SaveChangesAsync(ct);

        return Results.Ok(new { decision = ToDecisionDto(decision), result.Code, result.Reason });
    }

    private static async Task<IResult> GetProcedureEligibilityAsync(
        Guid caseId,
        HttpContext context,
        AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access,
        CancellationToken ct)
    {
        var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking()
            .Include(x => x.Evidence)
            .Include(x => x.Decisions)
            .Include(x => x.CommissionAppointments)
            .SingleOrDefaultAsync(x => x.Id == caseId, ct);
        if (admissionCase is null) return Results.NotFound();

        if (!access.CanReadLodgeSecretariat(context.User, admissionCase.OrganizationId) &&
            !access.CanEvaluateCeremonies(context.User))
            return Results.Forbid();

        var projection = AdmissionCaseEligibilityProjector.Evaluate(admissionCase);
        return Results.Ok(new
        {
            caseId,
            admissionCase.AdmissionType,
            status = projection.Decision.Status,
            canProceedToCeremonyRequest = projection.Decision.CanProceed,
            projection.Decision.Requirements,
            evidence = new
            {
                projection.WithdrawalLetterId,
                projection.Article23ReviewDecisionId,
                projection.FirstDegreePresentationDecisionId,
                projection.CommissionAppointmentGroupId,
                projection.CommissionCompletionDecisionId,
                projection.ThirdDegreeDecisionId,
                projection.FirstDegreeBallotDecisionId,
                projection.GrandMasterPardonDecisionId,
                projection.GrandMasterRegularityRecognitionDecisionId,
                projection.GrandMasterSpecialAcceptanceDecisionId
            }
        });
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

    private static bool DecisionReferencesAppointmentGroup(AdmissionDecision decision, Guid groupId)
    {
        if (string.IsNullOrWhiteSpace(decision.StructuredDataJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(decision.StructuredDataJson);
            return document.RootElement.TryGetProperty("appointmentGroupId", out var property) &&
                   property.ValueKind == JsonValueKind.String &&
                   Guid.TryParse(property.GetString(), out var parsed) &&
                   parsed == groupId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static AdmissionDecision? LatestDecision(AdmissionCase admissionCase, string decisionType)
        => admissionCase.Decisions
            .Where(x => x.DecisionType == decisionType)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefault();

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

public sealed record AdmissionFirstDegreePresentationRequest(
    DateOnly PresentationDate,
    string SourceReference,
    string? Notes);

public sealed record AdmissionThirdDegreeVoteRequest(
    DateOnly AsOfDate,
    string SourceReference,
    int PresentMasters,
    int VotesInFavor,
    int VotesAgainst,
    int Abstentions,
    string? Notes);

public sealed record AdmissionBallotRoundRequest(
    int ProcedureNumber,
    int EligibleVoters,
    int WhiteBallots,
    int BlackBallots);

public sealed record AdmissionFirstDegreeBallotRequest(
    DateOnly AsOfDate,
    string SourceReference,
    IReadOnlyCollection<AdmissionBallotRoundRequest>? Ballots,
    bool BallotApproved,
    string? Notes);
