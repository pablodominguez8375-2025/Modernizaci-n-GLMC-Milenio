using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

public static class AdvancementDispensationEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/solicitudes/{requestId:guid}/avance/dispensa", GetAsync);
        group.MapPost("/solicitudes/{requestId:guid}/avance/dispensa", SubmitAsync);
        group.MapPost("/solicitudes/{requestId:guid}/avance/dispensa/resolver", ResolveAsync);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
        DateTimeOffset.UtcNow, "America/Santiago").DateTime);
    private static string? Actor(HttpContext context) => context.User.FindFirst("sub")?.Value ??
        context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.Identity?.Name;

    private static async Task<IResult> GetAsync(Guid requestId, HttpContext http,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(http.User, ceremony.OrganizationId) &&
            !access.CanEvaluateCeremonies(http.User)) return Results.Forbid();
        var row = await db.AdvancementDispensationCases.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, ct);
        http.Response.Headers.CacheControl = "private, no-store";
        if (row is null) return Results.Ok(new { requestId, status = "not_requested" });
        return Results.Ok(new {
            requestId, row.Id, row.Foundation, row.MeetingId, row.FullMinuteId, row.BallotId,
            row.EligibleCount, row.PositiveCount, row.NegativeCount, row.Status,
            reductions = JsonSerializer.Deserialize<List<AdvancementDispensationReduction>>(row.ReductionsJson),
            row.SubmittedAtUtc, row.ResolvedAtUtc, row.ResolutionReference, row.ResolutionNotes
        });
    }

    private static async Task<IResult> SubmitAsync(Guid requestId, DispensationSubmission input,
        HttpContext http, PmgmDbContext db, LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(http.User, ceremony.OrganizationId)) return Results.Forbid();
        var actor = Actor(http);
        if (string.IsNullOrWhiteSpace(actor)) return Results.Forbid();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized ||
            ceremony.CeremonyType is not (CeremonyCodes.Type.WageIncrease or CeremonyCodes.Type.Exaltation) ||
            ceremony.MemberId is null) return Results.Conflict(new { message = "El expediente no admite dispensa." });
        if (string.IsNullOrWhiteSpace(input.Foundation) || input.Foundation.Trim().Length is < 20 or > 2000 ||
            input.MeetingId == Guid.Empty || input.FullMinuteId == Guid.Empty || input.BallotId == Guid.Empty ||
            input.EligibleMasterIds is null || input.Reductions is null)
            return Results.BadRequest(new { message = "Fundamento, Tenida, acta, votación, nómina y reducciones son obligatorios." });
        if (await db.AdvancementDispensationCases.AnyAsync(x => x.CeremonyRequestId == requestId, ct))
            return Results.Conflict(new { message = "La solicitud de dispensa ya existe y es inmutable." });

        var asOf = Today();
        var codes = new[] { AdvancementRulePolicy.CodeFor(ceremony.CeremonyType)!,
            AdvancementSeniorityRulePolicy.CodeFor(ceremony.CeremonyType)!,
            AdvancementDispensationRulePolicy.Code };
        var rules = await db.InstitutionalRuleSettings.AsNoTracking().Where(x =>
            codes.Contains(x.Code) && x.Status == "active" && x.EffectiveFrom <= asOf &&
            (x.EffectiveTo == null || x.EffectiveTo >= asOf)).ToListAsync(ct);
        var countRule = AdvancementRulePolicy.Resolve(ceremony.CeremonyType, asOf, rules);
        var seniorityRule = AdvancementSeniorityRulePolicy.Resolve(ceremony.CeremonyType, asOf, rules);
        var dispensationRule = AdvancementDispensationRulePolicy.Resolve(asOf, rules);
        var attendance = await AdvancementAttendanceProjection.GetAsync(ceremony.OrganizationId,
            ceremony.MemberId.Value, ceremony.CeremonyType, asOf, db, lodgeDb, ct);
        if (countRule is null || seniorityRule is null || dispensationRule is null ||
            attendance.Status != "ready" || attendance.Snapshot is null)
            return Results.Conflict(new { message = "Faltan reglas vigentes o evidencia institucional del grado." });
        var chronology = await AdvancementSeniorityProjection.GetAsync(ceremony.MemberId.Value,
            attendance.Snapshot.GradeStartDate, asOf, db, ct);
        var ballot = await lodgeDb.LodgeAnonymousBallots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.BallotId, ct);
        if (ballot is null) return Results.Conflict(new { message = "La votación indicada no existe." });
        var row = new AdvancementDispensationCase {
            CeremonyRequestId = requestId, OrganizationId = ceremony.OrganizationId,
            MemberId = ceremony.MemberId.Value, InstitutionalRuleId = dispensationRule.RuleId,
            AdvancementRuleId = countRule.RuleId, SeniorityRuleId = seniorityRule.RuleId,
            Foundation = input.Foundation.Trim(), MeetingId = input.MeetingId,
            FullMinuteId = input.FullMinuteId, BallotId = input.BallotId,
            EligibleMasterIdsJson = JsonSerializer.Serialize(input.EligibleMasterIds.Distinct().Order().ToArray()),
            ReductionsJson = JsonSerializer.Serialize(input.Reductions),
            EligibleCount = ballot.EligibleCount, PositiveCount = ballot.PositiveCount,
            NegativeCount = ballot.NegativeCount, Status = AdvancementDispensationCaseStatuses.CouncilApproved,
            SubmittedBySubject = actor
        };
        var check = await AdvancementDispensationEvidenceService.ValidateAsync(row, ceremony, asOf,
            countRule, seniorityRule, attendance.Snapshot, chronology, db, lodgeDb, ct);
        if (!check.Valid) return Results.Conflict(new { message = check.Reason });
        db.AdvancementDispensationCases.Add(row);
        audit.Add(http, "ceremony.advancement.dispensation.submitted",
            nameof(AdvancementDispensationCase), row.Id.ToString(), row.OrganizationId,
            AuditResults.Success, new { requestId, row.MeetingId, row.FullMinuteId, row.BallotId,
                row.EligibleCount, row.PositiveCount, row.NegativeCount, reductions = input.Reductions });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/ceremonias/solicitudes/{requestId}/avance/dispensa",
            new { row.Id, row.Status });
    }

    private static async Task<IResult> ResolveAsync(Guid requestId, DispensationResolution input,
        HttpContext http, PmgmDbContext db, LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanValidateCeremonyInternalAffairs(http.User)) return Results.Forbid();
        var actor = Actor(http);
        if (string.IsNullOrWhiteSpace(actor)) return Results.Forbid();
        var ceremony = await db.CeremonyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        var row = await db.AdvancementDispensationCases.SingleOrDefaultAsync(x => x.CeremonyRequestId == requestId, ct);
        if (ceremony is null || row is null) return Results.NotFound();
        if (row.Status != AdvancementDispensationCaseStatuses.CouncilApproved)
            return Results.Conflict(new { message = "La resolución es inmutable." });
        if (row.SubmittedBySubject == actor) return Results.Forbid();
        var reference = input.ResolutionReference?.Trim();
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 500 ||
            input.ResolutionNotes is { Length: > 2000 })
            return Results.BadRequest(new { message = "La resolución requiere referencia formal y observaciones válidas." });
        if (input.Approved)
        {
            var asOf = Today();
            var codes = new[] { AdvancementRulePolicy.CodeFor(ceremony.CeremonyType)!,
                AdvancementSeniorityRulePolicy.CodeFor(ceremony.CeremonyType)! };
            var rules = await db.InstitutionalRuleSettings.AsNoTracking().Where(x =>
                codes.Contains(x.Code) && x.Status == "active" && x.EffectiveFrom <= asOf &&
                (x.EffectiveTo == null || x.EffectiveTo >= asOf)).ToListAsync(ct);
            var countRule = AdvancementRulePolicy.Resolve(ceremony.CeremonyType, asOf, rules);
            var seniorityRule = AdvancementSeniorityRulePolicy.Resolve(ceremony.CeremonyType, asOf, rules);
            var attendance = ceremony.MemberId is null ? null : await AdvancementAttendanceProjection.GetAsync(
                ceremony.OrganizationId, ceremony.MemberId.Value, ceremony.CeremonyType, asOf, db, lodgeDb, ct);
            if (countRule is null || seniorityRule is null || attendance?.Status != "ready" ||
                attendance.Snapshot is null || ceremony.MemberId is null)
                return Results.Conflict(new { message = "La evidencia vigente no permite aprobar la dispensa." });
            var chronology = await AdvancementSeniorityProjection.GetAsync(ceremony.MemberId.Value,
                attendance.Snapshot.GradeStartDate, asOf, db, ct);
            var check = await AdvancementDispensationEvidenceService.ValidateAsync(row, ceremony, asOf,
                countRule, seniorityRule, attendance.Snapshot, chronology, db, lodgeDb, ct);
            if (!check.Valid) return Results.Conflict(new { message = check.Reason });
        }
        row.Status = input.Approved ? AdvancementDispensationCaseStatuses.Approved : AdvancementDispensationCaseStatuses.Rejected;
        row.ResolvedBySubject = actor; row.ResolvedAtUtc = DateTimeOffset.UtcNow;
        row.ResolutionReference = reference; row.ResolutionNotes = input.ResolutionNotes?.Trim();
        audit.Add(http, "ceremony.advancement.dispensation.resolved",
            nameof(AdvancementDispensationCase), row.Id.ToString(), row.OrganizationId,
            AuditResults.Success, new { requestId, row.Status, row.ResolutionReference, row.ResolvedAtUtc });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { row.Id, row.Status, row.ResolvedAtUtc });
    }
}

public sealed record DispensationSubmission(string Foundation, Guid MeetingId, Guid FullMinuteId,
    Guid BallotId, IReadOnlyList<Guid> EligibleMasterIds,
    IReadOnlyList<AdvancementDispensationReduction> Reductions);
public sealed record DispensationResolution(bool Approved, string ResolutionReference, string? ResolutionNotes);
