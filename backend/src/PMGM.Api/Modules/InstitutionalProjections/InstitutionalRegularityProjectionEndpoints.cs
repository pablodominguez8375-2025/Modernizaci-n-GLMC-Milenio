using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Treasury;

namespace PMGM.Api.Modules.InstitutionalProjections;

public static class InstitutionalRegularityProjectionEndpoints
{
    public static IEndpointRouteBuilder MapInstitutionalRegularityProjectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/institutional-projections")
            .WithTags("Proyecciones institucionales mínimas")
            .RequireAuthorization();

        group.MapGet("/talleres/{organizationId:guid}/ceremony-regularity", GetCeremonyRegularityAsync);

        return endpoints;
    }

    private static async Task<IResult> GetCeremonyRegularityAsync(
        Guid organizationId,
        DateOnly? asOf,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanEvaluateCeremonies(httpContext.User) &&
            !access.CanReadInstitutionalRegularity(httpContext.User) &&
            !access.CanReadOrganization(httpContext.User, organizationId))
        {
            return Results.Forbid();
        }

        var exists = await db.Organizations
            .AsNoTracking()
            .AnyAsync(x => x.Id == organizationId, cancellationToken);

        if (!exists)
        {
            return Results.NotFound(new { message = "El Taller u organización indicada no existe." });
        }

        var cutoff = asOf ?? TodayInChile();

        var treasury = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId &&
                x.MemberId == null &&
                x.Scope == TreasuryCodes.RegularityScope.Organization &&
                x.AsOfDate <= cutoff)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.Status, x.AsOfDate })
            .FirstOrDefaultAsync(cancellationToken);

        var hospitalaria = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AsOfDate <= cutoff)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.Status, x.AsOfDate })
            .FirstOrDefaultAsync(cancellationToken);

        var treasuryComplies = treasury?.Status is
            TreasuryCodes.RegularityStatus.UpToDate or
            TreasuryCodes.RegularityStatus.Exempt;

        var hospitalariaComplies = hospitalaria?.Status is
            HospitalariaCodes.RegularityStatus.UpToDate or
            HospitalariaCodes.RegularityStatus.Exempt;

        var projection = new CeremonyRegularityProjectionDto(
            cutoff,
            new InstitutionalRegularitySignalDto(
                treasury?.Status ?? "unknown",
                treasury?.AsOfDate,
                treasuryComplies),
            new InstitutionalRegularitySignalDto(
                hospitalaria?.Status ?? "unknown",
                hospitalaria?.AsOfDate,
                hospitalariaComplies),
            treasuryComplies && hospitalariaComplies);

        return Results.Ok(projection);
    }

    private static DateOnly TodayInChile()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var chileNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return DateOnly.FromDateTime(chileNow.DateTime);
    }
}

public sealed record CeremonyRegularityProjectionDto(
    DateOnly AsOfDate,
    InstitutionalRegularitySignalDto Treasury,
    InstitutionalRegularitySignalDto Hospitalaria,
    bool WorkshopComplies);

public sealed record InstitutionalRegularitySignalDto(
    string Status,
    DateOnly? SourceAsOfDate,
    bool Complies);
