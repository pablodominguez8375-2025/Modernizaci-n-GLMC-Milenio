using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.HonorTribunal.Entities;

namespace PMGM.Api.Modules.HonorTribunal;

public static class HonorTribunalEndpoints
{
    public static IEndpointRouteBuilder MapHonorTribunalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tribunal-honor")
            .WithTags("Tribunal de Honor")
            .RequireAuthorization();

        group.MapPost("/expedientes", CreateCaseAsync);
        group.MapPost("/expedientes/{caseId:guid}/sanciones", AddSanctionAsync);
        group.MapGet("/personas/{personId:guid}/efecto-institucional", GetInstitutionalEffectAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateCaseAsync(
        CreateHonorCaseRequest request,
        HttpContext httpContext,
        HonorTribunalDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!HonorTribunalAuthorization.CanManage(httpContext.User, access)) return Results.Forbid();
        if (request.PersonId == Guid.Empty || string.IsNullOrWhiteSpace(request.CaseNumber))
            return Results.BadRequest(new { message = "Persona y número de expediente son obligatorios." });

        var entity = new HonorCase
        {
            PersonId = request.PersonId,
            CaseNumber = request.CaseNumber.Trim(),
            OpenedOn = request.OpenedOn,
            Status = HonorTribunalCodes.CaseStatus.Open,
            Classification = string.IsNullOrWhiteSpace(request.Classification) ? "reserved" : request.Classification.Trim(),
            Reserved = true,
            CreatedBySubject = ActorSubject(httpContext.User)
        };

        db.HonorCases.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/tribunal-honor/expedientes/{entity.Id}", new { entity.Id, entity.CaseNumber, entity.Status });
    }

    private static async Task<IResult> AddSanctionAsync(
        Guid caseId,
        AddHonorSanctionRequest request,
        HttpContext httpContext,
        HonorTribunalDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!HonorTribunalAuthorization.CanManage(httpContext.User, access)) return Results.Forbid();
        var honorCase = await db.HonorCases.SingleOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (honorCase is null) return Results.NotFound();

        var sanction = new HonorSanction
        {
            HonorCaseId = honorCase.Id,
            PersonId = honorCase.PersonId,
            Type = request.Type.Trim(),
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            TotalLossOfRights = request.TotalLossOfRights,
            AffectsAttendance = request.AffectsAttendance,
            AffectsVoting = request.AffectsVoting,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Active = true,
            RecordedBySubject = ActorSubject(httpContext.User)
        };

        db.HonorSanctions.Add(sanction);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/tribunal-honor/expedientes/{caseId}/sanciones/{sanction.Id}", new { sanction.Id, sanction.Type, sanction.StartsOn, sanction.EndsOn });
    }

    private static async Task<IResult> GetInstitutionalEffectAsync(
        Guid personId,
        DateOnly? asOfDate,
        HttpContext httpContext,
        IHonorInstitutionalEffectService service,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!HonorTribunalAuthorization.CanReadInstitutionalEffect(httpContext.User, access)) return Results.Forbid();
        var date = asOfDate ?? ChileToday();
        var effect = await service.ResolveAsync(personId, date, cancellationToken);
        return Results.Ok(new
        {
            personId,
            asOfDate = date,
            effect.HasMasonicRights,
            effect.AttendanceRestricted,
            effect.VotingRestricted,
            effect.EffectReference
        });
    }

    private static string ActorSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.Identity?.Name ?? "unknown";

    private static DateOnly ChileToday()
    {
        var chile = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chile.DateTime);
    }
}

public sealed record CreateHonorCaseRequest(Guid PersonId, string CaseNumber, DateOnly OpenedOn, string? Classification);

public sealed record AddHonorSanctionRequest(
    string Type,
    DateOnly StartsOn,
    DateOnly? EndsOn,
    bool TotalLossOfRights,
    bool AffectsAttendance,
    bool AffectsVoting,
    string? Notes);
