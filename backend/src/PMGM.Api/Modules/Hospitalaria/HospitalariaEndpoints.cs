using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Hospitalaria.Entities;

namespace PMGM.Api.Modules.Hospitalaria;

public static class HospitalariaEndpoints
{
    public static IEndpointRouteBuilder MapHospitalariaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/hospitalaria")
            .WithTags("Gran Hospitalaria")
            .RequireAuthorization();

        group.MapPost("/talleres/{organizationId:guid}/regularidad", SetWorkshopRegularityAsync);
        group.MapGet("/talleres/{organizationId:guid}/regularidad", GetWorkshopRegularityAsync);

        return endpoints;
    }

    private static async Task<IResult> SetWorkshopRegularityAsync(
        Guid organizationId,
        HospitalariaRegularityRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageHospitalariaRegularity(httpContext.User))
        {
            return Results.Forbid();
        }

        if (!HospitalariaCodes.RegularityStatus.IsValid(request.Status))
        {
            return Results.BadRequest(new { message = "El estado de regularidad hospitalaria indicado no es válido." });
        }

        var organizationExists = await db.Organizations.AnyAsync(x => x.Id == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return Results.NotFound(new { message = "El Taller u organización no existe." });
        }

        var snapshot = new HospitalariaRegularitySnapshot
        {
            OrganizationId = organizationId,
            Status = request.Status,
            AsOfDate = request.AsOfDate,
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        db.HospitalariaRegularitySnapshots.Add(snapshot);
        audit.Add(
            httpContext,
            "hospitalaria.workshop_regularity.recorded",
            nameof(HospitalariaRegularitySnapshot),
            snapshot.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { snapshot.Status, snapshot.AsOfDate });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/hospitalaria/talleres/{organizationId}/regularidad",
            ToAdministrativeResponse(snapshot));
    }

    private static async Task<IResult> GetWorkshopRegularityAsync(
        Guid organizationId,
        DateOnly? asOf,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReadInstitutionalRegularity(httpContext.User) &&
            !access.CanReadOrganization(httpContext.User, organizationId))
        {
            return Results.Forbid();
        }

        var cutoff = asOf ?? TodayInChile();
        var snapshot = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AsOfDate <= cutoff)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return Results.NotFound(new { message = "No existe una validación de Gran Hospitalaria para la fecha indicada." });
        }

        if (access.CanManageHospitalariaRegularity(httpContext.User))
        {
            return Results.Ok(ToAdministrativeResponse(snapshot));
        }

        return Results.Ok(ToMinimalProjection(snapshot));
    }

    private static HospitalariaRegularityProjectionDto ToMinimalProjection(HospitalariaRegularitySnapshot snapshot)
        => new(snapshot.Status, snapshot.AsOfDate);

    private static object ToAdministrativeResponse(HospitalariaRegularitySnapshot snapshot) => new
    {
        snapshot.Id,
        snapshot.OrganizationId,
        snapshot.Status,
        snapshot.AsOfDate,
        snapshot.SourceReference,
        snapshot.Notes,
        snapshot.RecordedAtUtc
    };

    private static DateOnly TodayInChile()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var chileNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return DateOnly.FromDateTime(chileNow.DateTime);
    }
}

public sealed record HospitalariaRegularityProjectionDto(
    string Status,
    DateOnly AsOfDate);

public sealed record HospitalariaRegularityRequest(
    string Status,
    DateOnly AsOfDate,
    string? SourceReference,
    string? Notes);
