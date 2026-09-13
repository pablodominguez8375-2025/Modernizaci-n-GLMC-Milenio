using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static class LodgeHospitalariaEndpoints
{
    public static IEndpointRouteBuilder MapLodgeHospitalariaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial/hospitalaria")
            .WithTags("Hospitalaria del Taller").RequireAuthorization();
        group.MapPost("/talleres/{organizationId:guid}/movimientos", CreateMovementAsync);
        group.MapGet("/talleres/{organizationId:guid}/resumen", GetSummaryAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateMovementAsync(Guid organizationId, CreateLodgeHospitalariaMovementRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();
        if (request.Amount <= 0 || !HospitalariaMovementCodes.IsValidType(request.MovementType) || !HospitalariaMovementCodes.IsValidCategory(request.Category))
            return Results.BadRequest(new { message = "Tipo, categoría y monto del movimiento deben ser válidos." });
        var movement = new LodgeHospitalariaMovement { OrganizationId = organizationId, MovementType = request.MovementType,
            Category = request.Category, Amount = request.Amount, MovementDate = request.MovementDate,
            MemberReference = Normalize(request.MemberReference), Destination = Normalize(request.Destination),
            EvidenceReference = Normalize(request.EvidenceReference), Observation = Normalize(request.Observation),
            RecordedBySubject = context.User.FindFirstValue("sub") ?? "unknown" };
        db.LodgeHospitalariaMovements.Add(movement);
        audit.Add(context, "lodge.hospitalaria.movement.recorded", nameof(LodgeHospitalariaMovement), movement.Id.ToString(), organizationId,
            AuditResults.Success, new { movement.MovementType, movement.Category, movement.Amount, movement.MovementDate });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/gestion-logial/hospitalaria/movimientos/{movement.Id}", movement);
    }

    private static async Task<IResult> GetSummaryAsync(Guid organizationId, DateOnly? from, DateOnly? to, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();
        var start = from ?? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);
        var rows = await db.LodgeHospitalariaMovements.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.MovementDate >= start && x.MovementDate <= end).ToListAsync(ct);
        var income = rows.Where(x => x.MovementType == HospitalariaMovementCodes.Income).Sum(x => x.Amount);
        var expenses = rows.Where(x => x.MovementType == HospitalariaMovementCodes.Expense).Sum(x => x.Amount);
        return Results.Ok(new { organizationId, from = start, to = end, income, expenses, balance = income - expenses,
            movements = rows.Count, categories = rows.GroupBy(x => x.Category).Select(g => new { category = g.Key, total = g.Sum(x => x.Amount), count = g.Count() }), items = rows.OrderByDescending(x => x.MovementDate) });
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class HospitalariaMovementCodes
{
    public const string Income = "income";
    public const string Expense = "expense";
    public const string DeathReplenishment = "death_replenishment";
    public const string AnnualFund = "annual_replenishment_fund";
    public const string CharityBag = "charity_bag";
    public const string InitiationFee = "initiation_fee";
    public const string CharityAid = "charity_aid";
    public const string Supplies = "supplies";
    public const string Ceremony = "ceremony";
    public static bool IsValidType(string value) => value is Income or Expense;
    public static bool IsValidCategory(string value) => value is DeathReplenishment or AnnualFund or CharityBag or InitiationFee or CharityAid or Supplies or Ceremony;
}

public sealed record CreateLodgeHospitalariaMovementRequest(string MovementType, string Category, decimal Amount, DateOnly MovementDate,
    string? MemberReference, string? Destination, string? EvidenceReference, string? Observation);
