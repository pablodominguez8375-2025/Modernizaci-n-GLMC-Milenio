using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static class TreasuryEndpoints
{
    public static IEndpointRouteBuilder MapTreasuryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tesoreria")
            .WithTags("Gran Tesorería")
            .RequireAuthorization();

        group.MapGet("/tarifario-cuotas", GetOfficialFeeScheduleAsync);
        group.MapPost("/talleres/{organizationId:guid}/oriente", SetTreasuryTerritoryAsync);
        group.MapPost("/talleres/{organizationId:guid}/regularidad", SetWorkshopRegularityAsync);
        group.MapGet("/talleres/{organizationId:guid}/regularidad", GetWorkshopRegularityAsync);
        group.MapPost("/talleres/{organizationId:guid}/miembros/{memberId:guid}/regularidad", SetMemberRegularityAsync);
        group.MapGet("/talleres/{organizationId:guid}/miembros/{memberId:guid}/regularidad", GetMemberRegularityAsync);
        group.MapTreasuryStatementEndpoints();

        return endpoints;
    }

    private static IResult GetOfficialFeeScheduleAsync(DateOnly? asOf, IInstitutionalAccessService access, HttpContext context)
    {
        if (!access.CanManageTreasuryRegularity(context.User) && !access.CanReadInstitutionalRegularity(context.User)) return Results.Forbid();
        var date = asOf ?? TodayInChile();
        return Results.Ok(new { sourceReference = GrandTreasuryFeeSchedule.SourceReference, effectiveFrom = GrandTreasuryFeeSchedule.EffectiveFrom,
            asOf = date, items = GrandTreasuryFeeSchedule.Rates, ceremonyRights = GrandTreasuryFeeSchedule.CeremonyRights });
    }

    private static async Task<IResult> SetTreasuryTerritoryAsync(Guid organizationId, SetTreasuryTerritoryRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        if (!GrandTreasuryFeeSchedule.IsValidTerritory(request.Territory))
            return Results.BadRequest(new { message = "Seleccione Santiago, otro Oriente de Chile o Perú." });
        var organization = await db.Organizations.FirstOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        if (organization is null) return Results.NotFound();
        organization.TreasuryTerritory = request.Territory;
        audit.Add(context, "treasury.organization_territory.updated", nameof(Organization), organization.Id.ToString(), organization.Id,
            AuditResults.Success, new { organization.TreasuryTerritory });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { organizationId, territory = organization.TreasuryTerritory });
    }

    private static async Task<IResult> SetWorkshopRegularityAsync(
        Guid organizationId,
        RegularityRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(httpContext.User))
        {
            return Results.Forbid();
        }

        if (!TreasuryCodes.RegularityStatus.IsValid(request.Status))
        {
            return Results.BadRequest(new { message = "El estado de regularidad indicado no es válido." });
        }

        var organizationExists = await db.Organizations.AnyAsync(x => x.Id == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return Results.NotFound(new { message = "El Taller u organización no existe." });
        }

        var snapshot = new FinancialRegularitySnapshot
        {
            OrganizationId = organizationId,
            MemberId = null,
            Scope = TreasuryCodes.RegularityScope.Organization,
            Status = request.Status,
            AsOfDate = request.AsOfDate,
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        db.FinancialRegularitySnapshots.Add(snapshot);
        audit.Add(
            httpContext,
            "treasury.workshop_regularity.recorded",
            nameof(FinancialRegularitySnapshot),
            snapshot.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { snapshot.Scope, snapshot.Status, snapshot.AsOfDate });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/tesoreria/talleres/{organizationId}/regularidad",
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
        var snapshot = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId &&
                        x.MemberId == null &&
                        x.Scope == TreasuryCodes.RegularityScope.Organization &&
                        x.AsOfDate <= cutoff)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return Results.NotFound(new { message = "No existe una validación de regularidad del Taller para la fecha indicada." });
        }

        if (access.CanManageTreasuryRegularity(httpContext.User))
        {
            return Results.Ok(ToAdministrativeResponse(snapshot));
        }

        return Results.Ok(ToMinimalProjection(snapshot));
    }

    private static async Task<IResult> SetMemberRegularityAsync(
        Guid organizationId,
        Guid memberId,
        RegularityRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(httpContext.User))
        {
            return Results.Forbid();
        }

        if (!TreasuryCodes.RegularityStatus.IsValid(request.Status))
        {
            return Results.BadRequest(new { message = "El estado de regularidad indicado no es válido." });
        }

        var membershipExists = await db.Memberships.AnyAsync(
            x => x.MemberId == memberId && x.OrganizationId == organizationId,
            cancellationToken);

        if (!membershipExists)
        {
            return Results.NotFound(new { message = "El hermano no registra pertenencia al Taller indicado." });
        }

        var snapshot = new FinancialRegularitySnapshot
        {
            OrganizationId = organizationId,
            MemberId = memberId,
            Scope = TreasuryCodes.RegularityScope.Organization,
            Status = request.Status,
            AsOfDate = request.AsOfDate,
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        db.FinancialRegularitySnapshots.Add(snapshot);
        audit.Add(
            httpContext,
            "treasury.member_regularity.recorded",
            nameof(FinancialRegularitySnapshot),
            snapshot.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { snapshot.Scope, snapshot.Status, snapshot.AsOfDate });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/tesoreria/talleres/{organizationId}/miembros/{memberId}/regularidad",
            ToAdministrativeResponse(snapshot));
    }

    private static async Task<IResult> GetMemberRegularityAsync(
        Guid organizationId,
        Guid memberId,
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
        var snapshot = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId &&
                        x.MemberId == memberId &&
                        x.AsOfDate <= cutoff)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return Results.NotFound(new { message = "No existe una validación de regularidad del hermano para la fecha indicada." });
        }

        if (access.CanManageTreasuryRegularity(httpContext.User))
        {
            return Results.Ok(ToAdministrativeResponse(snapshot));
        }

        return Results.Ok(ToMinimalProjection(snapshot));
    }

    private static FinancialRegularityProjectionDto ToMinimalProjection(FinancialRegularitySnapshot snapshot)
        => new(snapshot.Status, snapshot.AsOfDate);

    private static object ToAdministrativeResponse(FinancialRegularitySnapshot snapshot) => new
    {
        snapshot.Id,
        snapshot.OrganizationId,
        snapshot.MemberId,
        snapshot.Scope,
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

public sealed record SetTreasuryTerritoryRequest(string Territory);

public sealed record FinancialRegularityProjectionDto(
    string Status,
    DateOnly AsOfDate);

public sealed record RegularityRequest(
    string Status,
    DateOnly AsOfDate,
    string? SourceReference,
    string? Notes);
