using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>Lectura y resolución de subsanaciones sólo a nivel Orden.</summary>
public static class CandidateRePresentationReviewEndpoints
{
    public static IEndpointRouteBuilder MapCandidateRePresentationReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/insinuados/regimen-interior/reinsinuaciones")
            .WithTags("Régimen Interior - nuevas presentaciones")
            .RequireAuthorization();
        group.MapGet("", GetReviewQueueAsync);
        group.MapPost("/{requestId:guid}/resolver", RecordReviewAsync);
        return endpoints;
    }

    private static bool CanReview(HttpContext context, IInstitutionalAccessService access)
        => access.HasOrderScope(context.User) &&
           access.HasRole(context.User, InstitutionalRoles.RegimenInterior, InstitutionalRoles.GranLogiaAdmin);

    private static async Task<IResult> GetReviewQueueAsync(
        HttpContext httpContext, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!CanReview(httpContext, access)) return Results.Forbid();
        var rows = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.ValidationType == CeremonyCodes.ValidationType.CandidateRepresentationReview &&
                        x.CeremonyRequest.CeremonyType == CeremonyCodes.Type.Initiation)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new
            {
                x.Id, x.CeremonyRequestId, x.Status, x.SourceReference, x.RecordedAtUtc,
                x.CeremonyRequest.OrganizationId,
                WorkshopName = x.CeremonyRequest.Organization.Name,
                RequestStatus = x.CeremonyRequest.Status,
                PersonName = x.CeremonyRequest.CandidatePerson == null ? "" :
                    x.CeremonyRequest.CandidatePerson.FirstNames + " " +
                    x.CeremonyRequest.CandidatePerson.LastNames
            })
            .Take(1500).ToListAsync(ct);
        var items = rows.GroupBy(x => x.CeremonyRequestId)
            .Select(group => new { Latest = group.First(), Original = group.Last() })
            .OrderByDescending(x => x.Latest.RecordedAtUtc)
            .Take(300)
            .Select(x => new
            {
                requestId = x.Latest.CeremonyRequestId, validationId = x.Latest.Id,
                x.Latest.OrganizationId, x.Latest.WorkshopName, x.Latest.PersonName,
                x.Latest.Status, x.Latest.SourceReference, x.Latest.RecordedAtUtc,
                requestStatus = x.Latest.RequestStatus,
                originalEvidenceId = x.Original.Id,
                originalSourceReference = x.Original.SourceReference
            }).ToArray();
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Length, items });
    }

    private static async Task<IResult> RecordReviewAsync(Guid requestId,
        CandidateRePresentationReviewRequest request, HttpContext httpContext,
        PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken ct)
    {
        if (!CanReview(httpContext, access)) return Results.Forbid();
        if (!CandidateRePresentationReviewPolicy.CanResolve(
                request.Status, request.SourceReference, request.Notes))
            return Results.BadRequest(new { message = "Registre decisión aprobar/observar/rechazar, referencia de resolución y observaciones válidas." });

        var ceremony = await db.CeremonyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (ceremony is null) return Results.NotFound();
        if (ceremony.CeremonyType != CeremonyCodes.Type.Initiation)
            return Results.BadRequest(new { message = "Esta revisión sólo aplica a nuevas presentaciones de Iniciación." });
        if (ceremony.Status is CeremonyCodes.RequestStatus.Authorized or CeremonyCodes.RequestStatus.Completed
            or CeremonyCodes.RequestStatus.Rejected)
            return Results.Conflict(new { message = "El expediente ya fue cerrado." });

        var initialEvidence = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.ValidationType == CeremonyCodes.ValidationType.CandidateRepresentationReview)
            .OrderBy(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (initialEvidence is null || initialEvidence.Status != CeremonyCodes.ValidationStatus.Observed ||
            string.IsNullOrWhiteSpace(initialEvidence.SourceReference))
            return Results.Conflict(new { message = "La nueva presentación no tiene una constancia inicial válida de Secretaría." });

        if (ceremony.CandidatePersonId is null)
            return Results.Conflict(new { message = "Falta la identidad institucional del expediente." });
        var priorRejection = await CandidateRePresentationGate.FindLatestAsync(
            db, ceremony.CandidatePersonId.Value, ct);
        if (priorRejection is null ||
            !CandidateIntakeWorkflowPolicy.EvaluateRePresentation(
                priorRejection.RejectionDate, ChileToday(), true).CanProceed)
            return Results.Conflict(new { message = "No se acreditó un rechazo histórico con un año reglamentario cumplido." });

        // Separación de funciones: ni Secretaría ni el creador del expediente pueden resolver su propio caso.
        var subject = httpContext.User.FindFirstValue("sub") ??
                      httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject)) return Results.Forbid();
        var creator = await db.AuditEvents.AsNoTracking()
            .Where(x => x.EntityType == nameof(CeremonyRequest) &&
                        x.EntityId == requestId.ToString() &&
                        (x.Action == "candidate.intake.created" || x.Action == "ceremony.request.created"))
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => x.ActorSubject).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(creator) || string.Equals(creator, subject, StringComparison.Ordinal))
            return Results.Forbid();

        var latest = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.ValidationType == CeremonyCodes.ValidationType.CandidateRepresentationReview)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstAsync(ct);
        var reference = request.SourceReference!.Trim();
        var notes = request.Notes?.Trim();
        if (latest.Status == request.Status &&
            string.Equals(latest.SourceReference, reference, StringComparison.Ordinal) &&
            string.Equals(latest.Notes, notes, StringComparison.Ordinal))
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { latest.Id, latest.Status, latest.SourceReference, replayed = true });
        }

        var validation = new CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = CeremonyCodes.ValidationType.CandidateRepresentationReview,
            Status = request.Status!,
            AsOfDate = ChileToday(),
            SourceReference = reference,
            Notes = notes
        };
        db.CeremonyValidations.Add(validation);
        audit.Add(httpContext, "candidate.representation.interior_review_recorded",
            nameof(CeremonyValidation), validation.Id.ToString(), ceremony.OrganizationId,
            request.Status == CeremonyCodes.ValidationStatus.Approved ? AuditResults.Success :
            request.Status == CeremonyCodes.ValidationStatus.Observed ? AuditResults.Observed : AuditResults.Rejected,
            new { requestId, sourceEvidenceId = initialEvidence.Id, previousReviewId = latest.Id,
                status = validation.Status, referenceRecorded = true });
        await db.SaveChangesAsync(ct);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { validation.Id, validation.Status, validation.SourceReference,
            validation.RecordedAtUtc, replayed = false });
    }

    private static DateOnly ChileToday() => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
}

public sealed record CandidateRePresentationReviewRequest(string? Status, string? SourceReference, string? Notes);
