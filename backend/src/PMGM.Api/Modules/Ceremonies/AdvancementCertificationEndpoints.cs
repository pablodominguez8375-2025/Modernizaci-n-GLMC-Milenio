using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

/// <summary>
/// Secretaría del Taller documenta la presentación. Régimen Interior resuelve
/// expresamente la verificación; ni documentos ni links emiten votos implícitos.
/// </summary>
public static class AdvancementCertificationEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/solicitudes/{requestId:guid}/avance/constancias", GetAsync);
        group.MapPost("/solicitudes/{requestId:guid}/avance/constancias", SubmitAsync);
        group.MapPost("/solicitudes/{requestId:guid}/avance/constancias/{attestationId:guid}/resolver", ResolveAsync);
        group.MapPost("/solicitudes/{requestId:guid}/avance/continuidad/validar", ValidateContinuityAsync);
    }

    private static DateOnly ChileToday()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);

    private static string? Actor(HttpContext context)
        => context.User.FindFirst("sub")?.Value ??
           context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
           context.User.Identity?.Name;

    private static async Task<AdvancementAttendanceResult> AttendanceAsync(
        CeremonyRequest ceremony, PmgmDbContext db,
        LodgeManagementDbContext lodgeDb, CancellationToken ct)
        => ceremony.MemberId is null
            ? new AdvancementAttendanceResult("member_missing", "Sin hermano identificado.", null)
            : await AdvancementAttendanceProjection.GetAsync(
                ceremony.OrganizationId, ceremony.MemberId.Value,
                ceremony.CeremonyType, ChileToday(), db, lodgeDb, ct);

    private static async Task<IResult> GetAsync(
        Guid requestId, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access,
        CancellationToken ct)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanEvaluateCeremonies(context.User) &&
            !access.CanManageLodgeSecretariat(context.User, ceremony.OrganizationId))
            return Results.Forbid();
        if (AdvancementAttendanceProjection.SourceGrade(ceremony.CeremonyType) is null)
            return Results.BadRequest();
        var rows = await db.AdvancementPaperAttestations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.OrganizationId == ceremony.OrganizationId &&
                        x.MemberId == ceremony.MemberId)
            .OrderByDescending(x => x.RecordedAtUtc).ToListAsync(ct);
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            requestId,
            records = rows.Select(x => new
            {
                x.Id, x.WorkPaperDocumentId, x.WorkPaperVersionId, x.WorkKind,
                x.MeetingId, x.ExtractVersionId, x.FullMinuteVersionId,
                x.PresentationDate, x.Status, x.CouncilApprovalReference,
                x.RecordedAtUtc, x.ReviewedAtUtc
            }),
            notice = "La aprobación del revisor no sustituye el control vivo documental y de continuidad al autorizar."
        });
    }

    private static async Task<IResult> SubmitAsync(
        Guid requestId, AdvancementPresentationSubmission input,
        HttpContext context, PmgmDbContext db,
        LodgeManagementDbContext lodgeDb, DocumentManagementDbContext documentsDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(context.User, ceremony.OrganizationId))
            return Results.Forbid();
        var actor = Actor(context);
        if (string.IsNullOrWhiteSpace(actor)) return Results.Forbid();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "No editar evidencias de una ceremonia autorizada." });
        if (AdvancementAttendanceProjection.SourceGrade(ceremony.CeremonyType) is null ||
            ceremony.MemberId is null || !AdvancementCertifiedPaperPolicy.ValidKind(input.WorkKind))
            return Results.BadRequest(new { message = "Ceremonia, hermano o clase de plancha no válida." });
        if (new[] { input.WorkPaperDocumentId, input.WorkPaperVersionId, input.MeetingId,
                     input.ExtractVersionId, input.FullMinuteVersionId }.Any(x => x == Guid.Empty))
            return Results.BadRequest(new { message = "No se admiten referencias documentales vacías." });

        var attendance = await AttendanceAsync(ceremony, db, lodgeDb, ct);
        if (attendance.Status != "ready" || attendance.Snapshot is null)
            return Results.Conflict(new { message = "No consta historial institucional válido del grado." });
        var recorded = new AdvancementPaperAttestation
        {
            CeremonyRequestId = requestId,
            OrganizationId = ceremony.OrganizationId,
            MemberId = ceremony.MemberId.Value,
            WorkPaperDocumentId = input.WorkPaperDocumentId,
            WorkPaperVersionId = input.WorkPaperVersionId,
            MeetingId = input.MeetingId,
            ExtractVersionId = input.ExtractVersionId,
            FullMinuteVersionId = input.FullMinuteVersionId,
            WorkKind = input.WorkKind,
            PresentationDate = input.PresentationDate,
            Status = "pending",
            PresentedBySubject = actor
        };
        if (!await AdvancementPaperEvidenceValidator.IsLiveAsync(recorded, ceremony.CeremonyType,
            attendance.Snapshot.GradeStartDate, ChileToday(), db, lodgeDb, documentsDb, ct))
            return Results.Conflict(new { message = "La evidencia no coincide con el Taller, Hermano, grado, fecha, Tenida celebrada y documentos íntegros." });

        var repeated = await db.AdvancementPaperAttestations.AsNoTracking().AnyAsync(x =>
            x.CeremonyRequestId == requestId &&
            x.WorkPaperDocumentId == input.WorkPaperDocumentId &&
            x.WorkPaperVersionId == input.WorkPaperVersionId &&
            x.Status != "rejected", ct);
        if (repeated)
            return Results.Conflict(new { message = "La misma versión ya tiene constancia vigente o pendiente." });

        db.AdvancementPaperAttestations.Add(recorded);
        audit.Add(context, "ceremony.advancement.presentation.submitted",
            nameof(AdvancementPaperAttestation), recorded.Id.ToString(),
            ceremony.OrganizationId, AuditResults.Success, new {
                requestId, input.WorkPaperDocumentId, input.WorkPaperVersionId,
                input.MeetingId, input.PresentationDate, input.WorkKind
            });
        await db.SaveChangesAsync(ct);
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Created($"/api/ceremonias/solicitudes/{requestId}/avance/constancias",
            new { recorded.Id, recorded.Status, recorded.WorkKind });
    }

    private static async Task<IResult> ResolveAsync(
        Guid requestId, Guid attestationId, AdvancementAttestationReview review,
        HttpContext context, PmgmDbContext db,
        LodgeManagementDbContext lodgeDb, DocumentManagementDbContext documentsDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanValidateCeremonyInternalAffairs(context.User))
            return Results.Forbid();
        var actor = Actor(context);
        if (string.IsNullOrWhiteSpace(actor)) return Results.Forbid();
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (ceremony is null) return Results.NotFound();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia ya fue autorizada." });
        var row = await db.AdvancementPaperAttestations
            .SingleOrDefaultAsync(x => x.Id == attestationId &&
                x.CeremonyRequestId == requestId &&
                x.OrganizationId == ceremony.OrganizationId && x.MemberId == ceremony.MemberId, ct);
        if (row is null) return Results.NotFound();
        if (row.Status != "pending")
            return Results.Conflict(new { message = "La constancia ya fue resuelta; su revisión es inmutable." });
        if (actor == row.PresentedBySubject)
            return Results.Forbid();
        var reference = review.CouncilApprovalReference?.Trim();
        if (review.Approved && (string.IsNullOrWhiteSpace(reference) || reference.Length > 500))
            return Results.BadRequest(new { message = "La aprobación exige referencia del acuerdo/acta de Cámara del Medio (máximo 500)." });
        if (review.ReviewNotes is { Length: > 2000 })
            return Results.BadRequest(new { message = "Observaciones demasiado extensas." });
        var attendance = await AttendanceAsync(ceremony, db, lodgeDb, ct);
        if (attendance.Status != "ready" || attendance.Snapshot is null ||
            !await AdvancementPaperEvidenceValidator.IsLiveAsync(row, ceremony.CeremonyType,
                attendance.Snapshot.GradeStartDate, ChileToday(), db, lodgeDb, documentsDb, ct))
            return Results.Conflict(new { message = "Evidencia documental/cronológica dejó de ser verificable; no puede resolverse." });

        row.Status = review.Approved ? "approved" : "rejected";
        row.CouncilApprovalReference = review.Approved ? reference : null;
        row.ReviewedBySubject = actor;
        row.ReviewedAtUtc = DateTimeOffset.UtcNow;
        row.ReviewNotes = review.ReviewNotes?.Trim();
        audit.Add(context, "ceremony.advancement.presentation.reviewed",
            nameof(AdvancementPaperAttestation), row.Id.ToString(),
            ceremony.OrganizationId, AuditResults.Success,
            new { requestId, row.Status, row.WorkPaperDocumentId, row.MeetingId,
                row.CouncilApprovalReference, row.ReviewedAtUtc });
        await db.SaveChangesAsync(ct);
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { row.Id, row.Status, row.ReviewedAtUtc });
    }

    private static async Task<IResult> ValidateContinuityAsync(
        Guid requestId, AdvancementContinuityReview review,
        HttpContext context, PmgmDbContext db, LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanValidateCeremonyInternalAffairs(context.User))
            return Results.Forbid();
        if (string.IsNullOrWhiteSpace(Actor(context))) return Results.Forbid();
        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (ceremony is null) return Results.NotFound();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "No modificar una ceremonia autorizada." });
        if (AdvancementSeniorityRulePolicy.CodeFor(ceremony.CeremonyType) is null ||
            ceremony.MemberId is null)
            return Results.BadRequest();
        var note = review.Reference?.Trim();
        if (string.IsNullOrWhiteSpace(note) || note.Length > 500)
            return Results.BadRequest(new { message = "La decisión requiere referencia formal del respaldo (máximo 500)." });
        var asOf = ChileToday();
        var ruleCode = AdvancementSeniorityRulePolicy.CodeFor(ceremony.CeremonyType)!;
        var rules = await db.InstitutionalRuleSettings.AsNoTracking().Where(x =>
            x.Code == ruleCode && x.Status == "active" && x.EffectiveFrom <= asOf &&
            (x.EffectiveTo == null || x.EffectiveTo >= asOf)).ToListAsync(ct);
        var rule = AdvancementSeniorityRulePolicy.Resolve(ceremony.CeremonyType, asOf, rules);
        var attendance = await AttendanceAsync(ceremony, db, lodgeDb, ct);
        if (rule is null || attendance.Status != "ready" || attendance.Snapshot is null)
            return Results.Conflict(new { message = "No existe regla o cronología válida." });
        var chronology = await AdvancementSeniorityProjection.GetAsync(ceremony.MemberId.Value,
            attendance.Snapshot.GradeStartDate, asOf, db, ct);
        var check = AdvancementSeniorityRulePolicy.Review(rule, chronology);
        if (review.Approved && !check.ChronologicalThresholdReached)
            return Results.Conflict(new { message = "La antigüedad y continuidad no alcanzan mínimos verificables." });
        var snapshot = new CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = "advancement_continuity",
            Status = review.Approved ? CeremonyCodes.ValidationStatus.Approved : CeremonyCodes.ValidationStatus.Rejected,
            AsOfDate = asOf,
            SourceReference = $"{rule.RuleId:N}|{attendance.Snapshot.GradeStartDate:yyyy-MM-dd}",
            Notes = note
        };
        db.CeremonyValidations.Add(snapshot);
        audit.Add(context, "ceremony.advancement.continuity.reviewed",
            nameof(CeremonyValidation), snapshot.Id.ToString(),
            ceremony.OrganizationId, AuditResults.Success,
            new { requestId, snapshot.Status, snapshot.AsOfDate, snapshot.SourceReference });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { snapshot.Id, snapshot.Status, snapshot.AsOfDate });
    }
}

public sealed record AdvancementPresentationSubmission(
    Guid WorkPaperDocumentId, Guid WorkPaperVersionId,
    Guid MeetingId, Guid ExtractVersionId, Guid FullMinuteVersionId,
    string WorkKind, DateOnly PresentationDate);

public sealed record AdvancementAttestationReview(
    bool Approved, string? CouncilApprovalReference, string? ReviewNotes);

public sealed record AdvancementContinuityReview(bool Approved, string? Reference);
