using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.CandidateIntake;

/// <summary>
/// #613 C3: corrección privada con trazabilidad irreversible. No modifica el
/// origen por PUT de ficha genérico ni expone historial en consultas públicas.
/// </summary>
public static class CandidateOriginCorrectionEndpoints
{
    public static void Map(RouteGroupBuilder group)
        => group.MapPut("/solicitudes/{requestId:guid}/origen/corregir", CorrectAsync);

    private static async Task<IResult> CorrectAsync(
        Guid requestId, CorrectCandidateOriginRequest input,
        HttpContext http, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit,
        CancellationToken ct)
    {
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length is < 10 or > 500 ||
            reason.Any(char.IsControl))
            return Results.BadRequest(new {
                code = "CANDIDATE_ORIGIN_CORRECTION_REASON_REQUIRED",
                message = "Indique motivo de corrección de 10 a 500 caracteres, sin caracteres de control."
            });

        var actor = http.User.FindFirstValue("sub") ??
            http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Results.Forbid();

        var scope = await db.CeremonyRequests.AsNoTracking()
            .Where(x => x.Id == requestId && x.CeremonyType == CeremonyCodes.Type.Initiation)
            .Select(x => new { x.OrganizationId })
            .SingleOrDefaultAsync(ct);
        if (scope is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(http.User, scope.OrganizationId))
            return Results.Forbid();

        // El mismo candado se usa al designar entrevistadores. La lectura de
        // la identidad y las designaciones ocurre después de adquirirlo.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({requestId.ToString()}, 0))", ct);
        var ceremony = await db.CeremonyRequests
            .SingleOrDefaultAsync(x => x.Id == requestId &&
                x.CeremonyType == CeremonyCodes.Type.Initiation, ct);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(http.User, ceremony.OrganizationId))
            return Results.Forbid();
        if (ceremony.Status is CeremonyCodes.RequestStatus.Authorized or
            CeremonyCodes.RequestStatus.Completed or CeremonyCodes.RequestStatus.Rejected)
            return Results.Conflict(new {
                code = "CANDIDATE_ORIGIN_CASE_CLOSED",
                message = "El expediente cerrado no admite corrección de origen."
            });

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var verified = input.OriginKind == CandidateOriginPolicy.Brother &&
            input.RecommenderMemberId is Guid recommenderId
            ? await CandidateOriginAccreditation.VerifyAsync(db, recommenderId, today, ct)
            : null;
        var origin = CandidateOriginPolicy.ValidateNew(
            input.OriginKind, input.RecommenderMemberId,
            input.OriginOtherDescription, verified, today);
        if (!origin.IsValid)
            return Results.BadRequest(new {
                code = origin.ErrorCode,
                message = "El origen no es válido o el hermano recomendante no está acreditado."
            });

        if (ceremony.CandidateOriginKind == origin.Kind &&
            ceremony.CandidateRecommenderMemberId == origin.RecommenderMemberId &&
            ceremony.CandidateOriginOtherDescription == origin.OtherDescription)
            return Results.Conflict(new {
                code = "CANDIDATE_ORIGIN_UNCHANGED",
                message = "No existe cambio de origen que justifique una corrección."
            });

        // Ni siquiera una designación histórica reemplazada debe coincidir
        // con quien se declarará como recomendante.
        if (origin.RecommenderMemberId is Guid newRecommender &&
            await db.CandidateInterviewAssignments.AsNoTracking().AnyAsync(
                x => x.CeremonyRequestId == requestId &&
                     x.InterviewerMemberId == newRecommender, ct))
            return Results.Conflict(new {
                code = "CANDIDATE_RECOMMENDER_INTERVIEW_CONFLICT",
                message = "Este hermano figura entre las designaciones de entrevistadores del expediente."
            });

        var correction = new CandidateOriginCorrection
        {
            CeremonyRequestId = requestId,
            PreviousKind = ceremony.CandidateOriginKind,
            PreviousRecommenderMemberId = ceremony.CandidateRecommenderMemberId,
            PreviousOtherDescription = ceremony.CandidateOriginOtherDescription,
            NewKind = origin.Kind!,
            NewRecommenderMemberId = origin.RecommenderMemberId,
            NewOtherDescription = origin.OtherDescription,
            Reason = reason,
            ChangedBySubject = actor
        };
        db.CandidateOriginCorrections.Add(correction);
        ceremony.CandidateOriginKind = origin.Kind;
        ceremony.CandidateRecommenderMemberId = origin.RecommenderMemberId;
        ceremony.CandidateOriginOtherDescription = origin.OtherDescription;
        audit.Add(http, "candidate.origin.corrected", nameof(CeremonyRequest),
            requestId.ToString("D"), ceremony.OrganizationId, AuditResults.Success,
            new {
                correctionId = correction.Id,
                previousOriginKind = correction.PreviousKind,
                newOriginKind = correction.NewKind,
                recommenderChanged =
                    correction.PreviousRecommenderMemberId != correction.NewRecommenderMemberId,
                reasonRecorded = true
            });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        http.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new {
            ceremonyRequestId = requestId, correctionId = correction.Id,
            originKind = origin.Kind, recommenderMemberId = origin.RecommenderMemberId,
            originOtherDescription = origin.OtherDescription,
            changedAtUtc = correction.ChangedAtUtc
        });
    }
}

public sealed record CorrectCandidateOriginRequest(
    string? OriginKind, Guid? RecommenderMemberId,
    string? OriginOtherDescription, string? Reason);
