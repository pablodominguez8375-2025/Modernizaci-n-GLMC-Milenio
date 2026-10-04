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
        endpoints.MapGrandTreasuryTariffEndpoints();

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
        var unpriced = new List<object>();
        foreach (var ceremony in ceremonies)
        {
            paidById.TryGetValue(ceremony.Id, out var paid);
            var right = await GrandTreasuryTariff.ResolveCeremonyAsync(db, ceremony.Id, ceremony.OrganizationId, ceremony.CeremonyType, today, cancellationToken);
            if (right is null)
            {
                if (GrandTreasuryFeeSchedule.IsChargedCeremony(ceremony.CeremonyType)) unpriced.Add(new { ceremony.Id, ceremony.OrganizationId, organizationName = ceremony.organizationName, ceremony.CeremonyType, reason = "Complete la Ficha y el tarifario vigente para calcular este derecho; no admite pagos sin tarifa." });
                continue;
            }
            var balance = Math.Max(0m, right.Value.Amount - paid);
            if (balance <= 0m) continue;
            items.Add(new TreasuryCeremonyRightItem(ceremony.Id, ceremony.OrganizationId, ceremony.organizationName,
                ceremony.organizationNumber, ceremony.CeremonyType, ceremony.ProposedDate, ceremony.subjectDisplayName,
                right.Value.Amount, right.Value.Currency, paid, balance, right.Value.SourceReference));
        }
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items, unpriced });
    }

    private static async Task<IResult> GetOfficialFeeScheduleAsync(DateOnly? asOf, Guid? organizationId,
        IInstitutionalAccessService access, HttpContext context, PmgmDbContext db, CancellationToken cancellationToken)
    {
        var institution = access.CanManageTreasuryRegularity(context.User) || access.CanReadInstitutionalRegularity(context.User);
        if (!institution && (organizationId is null ||
            (!access.CanManageLodgeTreasury(context.User, organizationId.Value) && !access.CanApproveLodgeExpenses(context.User, organizationId.Value)))) return Results.Forbid();
        if (!institution && !await DynamicTreasuryAccess.AllowsAsync(db, context.User, organizationId!.Value, "view", cancellationToken)) return Results.Forbid();
        var date = asOf ?? TodayInChile();
        var tariff = GrandTreasuryTariff.At(await GrandTreasuryTariff.LoadAsync(db, cancellationToken), date);
        if (tariff is null) return Results.Conflict(new { message = "No existe un decreto publicado vigente para la fecha indicada." });
        string? territory = null;
        if (organizationId is not null)
        {
            territory = await WorkshopOriente.TerritoryAsync(db, organizationId.Value, cancellationToken);
            if (territory is null) return Results.Conflict(new { message = "Complete el Oriente y país en la Ficha del Taller." });
        }
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { tariff.Id, tariff.Version, tariff.Number, tariff.SourceReference, tariff.DecreeDate,
            tariff.EffectiveFrom, tariff.EffectiveUntil, asOf = date, territory,
            items = tariff.Rates.Where(x => territory is null || x.Territory == territory),
            ceremonyRights = tariff.CeremonyRights.Where(x => territory is null || x.Territory == territory),
            unemployment = tariff.Unemployment.Where(x => territory is null || x.Territory == territory) });
    }

    private static async Task<IResult> GetTreasuryTerritoriesAsync(HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        var rows = await db.Organizations.AsNoTracking().Where(x => x.Type != "order")
            .OrderBy(x => x.Name).ThenBy(x => x.Number).ToListAsync(cancellationToken);
        var items = rows.Select(x => new { x.Id, x.Name, x.Number, x.Type, x.City, x.Country, x.OrienteCode,
            treasuryTerritory = WorkshopOriente.Territory(x) }).ToList();
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> GetTreasuryTerritoryAsync(Guid organizationId, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User) &&
            !access.CanManageLodgeTreasury(context.User, organizationId) && !access.CanApproveLodgeExpenses(context.User, organizationId)) return Results.Forbid();
        var item = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        return item is null ? Results.NotFound() : Results.Ok(new { organizationId = item.Id,
            territory = WorkshopOriente.Territory(item), item.City, item.Country, item.OrienteCode });
    }

    private static async Task<IResult> SetTreasuryTerritoryAsync(Guid organizationId, SetTreasuryTerritoryRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        if (!await db.Organizations.AnyAsync(x => x.Id == organizationId, cancellationToken)) return Results.NotFound();
        return Results.Conflict(new { message = "El Oriente se administra exclusivamente en la Ficha del Taller; esta consulta es de solo lectura." });
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
