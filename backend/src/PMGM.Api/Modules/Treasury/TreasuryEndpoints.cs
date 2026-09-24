using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
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
        group.MapGet("/derechos-ceremoniales", GetCeremonyRightsAsync);
        group.MapGet("/talleres/orientes", GetTreasuryTerritoriesAsync);
        group.MapGet("/talleres/{organizationId:guid}/oriente", GetTreasuryTerritoryAsync);
        group.MapPost("/talleres/{organizationId:guid}/oriente", SetTreasuryTerritoryAsync);
        group.MapPost("/talleres/{organizationId:guid}/regularidad", SetWorkshopRegularityAsync);
        group.MapGet("/talleres/{organizationId:guid}/regularidad", GetWorkshopRegularityAsync);
        group.MapPost("/talleres/{organizationId:guid}/miembros/{memberId:guid}/regularidad", SetMemberRegularityAsync);
        group.MapGet("/talleres/{organizationId:guid}/miembros/{memberId:guid}/regularidad", GetMemberRegularityAsync);
        group.MapTreasuryStatementEndpoints();

        return endpoints;
    }

    private static async Task<IResult> GetCeremonyRightsAsync(HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        var today = TodayInChile();
        var ceremonies = await db.CeremonyRequests.AsNoTracking()
            .Where(x => x.Status != CeremonyCodes.RequestStatus.Rejected && x.Status != CeremonyCodes.RequestStatus.Completed)
            .OrderBy(x => x.ProposedDate).ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id, x.OrganizationId, organizationName = x.Organization.Name, organizationNumber = x.Organization.Number,
                x.CeremonyType, x.ProposedDate,
                subjectDisplayName = x.CandidatePerson != null
                    ? (x.CandidatePerson.FirstNames + " " + x.CandidatePerson.LastNames).Trim()
                    : x.Member != null ? (x.Member.Person.FirstNames + " " + x.Member.Person.LastNames).Trim() : "Persona no asociada"
            }).Take(500).ToListAsync(cancellationToken);
        if (ceremonies.Count == 0)
        {
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { total = 0, items = Array.Empty<object>() });
        }

        var ids = ceremonies.Select(x => x.Id).ToArray();
        var paidRows = await db.CeremonyRightPayments.AsNoTracking().Where(x => ids.Contains(x.CeremonyRequestId) && x.PaymentDate <= today)
            .GroupBy(x => x.CeremonyRequestId).Select(x => new { Id = x.Key, Paid = x.Sum(y => y.Amount) }).ToListAsync(cancellationToken);
        var paidById = paidRows.ToDictionary(x => x.Id, x => x.Paid);
        var items = new List<TreasuryCeremonyRightItem>();
        foreach (var ceremony in ceremonies)
        {
            paidById.TryGetValue(ceremony.Id, out var paid);
            var right = GrandTreasuryFeeSchedule.ResolveCeremonyRight(ceremony.CeremonyType, today);
            if (right is null) continue;
            var balance = Math.Max(0m, right.Value.Amount - paid);
            if (balance <= 0m) continue;
            items.Add(new TreasuryCeremonyRightItem(ceremony.Id, ceremony.OrganizationId, ceremony.organizationName,
                ceremony.organizationNumber, ceremony.CeremonyType, ceremony.ProposedDate, ceremony.subjectDisplayName,
                right.Value.Amount, right.Value.Currency, paid, balance, GrandTreasuryFeeSchedule.SourceReference));
        }
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static IResult GetOfficialFeeScheduleAsync(DateOnly? asOf, IInstitutionalAccessService access, HttpContext context)
    {
        if (!access.CanManageTreasuryRegularity(context.User) && !access.CanReadInstitutionalRegularity(context.User)) return Results.Forbid();
        var date = asOf ?? TodayInChile();
        return Results.Ok(new { sourceReference = GrandTreasuryFeeSchedule.SourceReference, effectiveFrom = GrandTreasuryFeeSchedule.EffectiveFrom,
            asOf = date, items = GrandTreasuryFeeSchedule.Rates, ceremonyRights = GrandTreasuryFeeSchedule.CeremonyRights });
    }

    private static async Task<IResult> GetTreasuryTerritoriesAsync(HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        var items = await db.Organizations.AsNoTracking().Where(x => x.Type != "order")
            .OrderBy(x => x.Name).ThenBy(x => x.Number)
            .Select(x => new { x.Id, x.Name, x.Number, x.Type, x.TreasuryTerritory })
            .ToListAsync(cancellationToken);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> GetTreasuryTerritoryAsync(Guid organizationId, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User) &&
            !access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        var item = await db.Organizations.AsNoTracking().Where(x => x.Id == organizationId)
            .Select(x => new { organizationId = x.Id, territory = x.TreasuryTerritory })
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(item);
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

public sealed record TreasuryCeremonyRightItem(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string? OrganizationNumber,
    string CeremonyType,
    DateOnly? ProposedDate,
    string SubjectDisplayName,
    decimal Amount,
    string Currency,
    decimal Paid,
    decimal Balance,
    string Source);
