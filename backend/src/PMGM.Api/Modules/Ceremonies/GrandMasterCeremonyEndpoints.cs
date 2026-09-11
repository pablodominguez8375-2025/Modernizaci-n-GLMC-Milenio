using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;

namespace PMGM.Api.Modules.Ceremonies;

public static class GrandMasterCeremonyEndpoints
{
    public static IEndpointRouteBuilder MapGrandMasterCeremonyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ceremonias")
            .WithTags("Ceremonias - Gran Maestría")
            .RequireAuthorization();

        group.MapPost("/solicitudes/{requestId:guid}/validaciones/gran-maestria", SetGrandMasterValidationAsync);
        group.MapGet("/solicitudes/{requestId:guid}/validaciones/gran-maestria", GetGrandMasterValidationAsync);
        return endpoints;
    }

    private static async Task<IResult> SetGrandMasterValidationAsync(
        Guid requestId,
        GrandMasterValidationRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanProvideGrandMasterApproval(httpContext.User)) return Results.Forbid();

        if (request.Status is not CeremonyCodes.ValidationStatus.Approved
            and not CeremonyCodes.ValidationStatus.Observed
            and not CeremonyCodes.ValidationStatus.Rejected)
            return Results.BadRequest(new { message = "Estado de visto bueno de Gran Maestría no válido." });

        var ceremony = await db.CeremonyRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null) return Results.NotFound();
        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia ya se encuentra autorizada." });

        var validation = new CeremonyValidation
        {
            CeremonyRequestId = ceremony.Id,
            ValidationType = CeremonyCodes.ValidationType.GrandMaster,
            Status = request.Status,
            AsOfDate = ChileToday(),
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        db.CeremonyValidations.Add(validation);
        audit.Add(
            httpContext,
            "ceremony.grand_master_validation.recorded",
            nameof(CeremonyValidation),
            validation.Id.ToString(),
            ceremony.OrganizationId,
            request.Status == CeremonyCodes.ValidationStatus.Approved ? AuditResults.Success : AuditResults.Rejected,
            new { validation.Status, validation.AsOfDate, validation.SourceReference });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new
        {
            validation.Id,
            validation.CeremonyRequestId,
            validation.ValidationType,
            validation.Status,
            validation.AsOfDate,
            validation.SourceReference,
            validation.RecordedAtUtc
        });
    }

    private static async Task<IResult> GetGrandMasterValidationAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanEvaluateCeremonies(httpContext.User) && !access.CanReadOrganization(httpContext.User, ceremony.OrganizationId))
            return Results.Forbid();

        var validation = await db.CeremonyValidations.AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId && x.ValidationType == CeremonyCodes.ValidationType.GrandMaster)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (validation is null)
            return Results.Ok(new
            {
                requestId,
                validationType = CeremonyCodes.ValidationType.GrandMaster,
                status = CeremonyCodes.ValidationStatus.Pending,
                canAuthorize = false
            });

        return Results.Ok(new
        {
            requestId,
            validation.Id,
            validation.ValidationType,
            validation.Status,
            validation.AsOfDate,
            validation.SourceReference,
            validation.Notes,
            validation.RecordedAtUtc,
            canAuthorize = GrandMasterCeremonyAuthorizationPolicy.CanAuthorize(validation.Status)
        });
    }

    private static DateOnly ChileToday()
    {
        var chileNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chileNow.DateTime);
    }
}

public sealed record GrandMasterValidationRequest(string Status, string? SourceReference, string? Notes);

public static class GrandMasterCeremonyAuthorizationPolicy
{
    public static bool CanAuthorize(string? status)
        => string.Equals(status, CeremonyCodes.ValidationStatus.Approved, StringComparison.OrdinalIgnoreCase);
}
