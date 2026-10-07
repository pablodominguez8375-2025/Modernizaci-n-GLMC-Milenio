using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Hospitalaria.Entities;
namespace PMGM.Api.Modules.Hospitalaria;

public static class HospitalariaContributionEndpoints
{
    public static IEndpointRouteBuilder MapHospitalariaContributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/hospitalaria/aportes").WithTags("Aporte mensual por Taller").RequireAuthorization();
        group.MapGet("/tarifas", RatesAsync); group.MapPost("/tarifas", SetRateAsync);
        group.MapGet("", ListAsync); group.MapPost("/{id:guid}/pago", PayAsync); group.MapPost("/{id:guid}/revision", ReviewAsync);
        return endpoints;
    }
    private static string Subject(HttpContext http) => http.User.FindFirstValue("sub") ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private static async Task<bool> Grand(HttpContext h, PmgmDbContext db, IInstitutionalAccessService a, string action, CancellationToken ct)
        => a.CanManageHospitalariaRegularity(h.User) && await DynamicGrandHospitalariaAccess.AllowsAsync(db, h.User, action, ct);
    private static async Task<IResult> RatesAsync(HttpContext h, PmgmDbContext db, IInstitutionalAccessService a, CancellationToken ct)
    {
        if (!await Grand(h, db, a, "view", ct)) return Results.Forbid();
        h.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { items = await db.HospitalariaContributionRates.AsNoTracking().OrderByDescending(x => x.EffectiveFrom)
            .Select(x => new { x.Id, x.Amount, x.EffectiveFrom, x.EffectiveUntil }).ToListAsync(ct) });
    }
    private static async Task<IResult> SetRateAsync(ContributionRateRequest r, HttpContext h, PmgmDbContext db, IInstitutionalAccessService a, IAuditService audit, CancellationToken ct)
    {
        if (!await Grand(h, db, a, "create", ct)) return Results.Forbid();
        if (r.Amount <= 0 || r.Amount != decimal.Truncate(r.Amount) || r.EffectiveFrom < HospitalariaContributionGenerator.Today())
            return Results.BadRequest(new { message = "Monto entero positivo y vigencia desde hoy o posterior." });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3546000)", ct);
        if (await db.HospitalariaContributionRates.AnyAsync(x => x.EffectiveFrom >= r.EffectiveFrom, ct))
            return Results.Conflict(new { message = "Existe una versión en esa fecha o posterior." });
        var previous = await db.HospitalariaContributionRates.OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (previous != null) previous.EffectiveUntil = r.EffectiveFrom.AddDays(-1);
        var rate = new HospitalariaContributionRate { Amount = r.Amount, EffectiveFrom = r.EffectiveFrom, CreatedBySubject = Subject(h) };
        db.HospitalariaContributionRates.Add(rate);
        audit.Add(h, "hospitalaria.contribution_rate.created", nameof(HospitalariaContributionRate), rate.Id.ToString(), null, AuditResults.Success,
            new { rate.Amount, rate.EffectiveFrom, previousRateId = previous?.Id });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await HospitalariaContributionGenerator.GenerateAsync(db, ct);
        return Results.Created("/api/hospitalaria/aportes/tarifas", new { rate.Id, rate.Amount, rate.EffectiveFrom });
    }
    private static async Task<IResult> ListAsync(Guid? organizationId, int? year, int? month, HttpContext h, PmgmDbContext db, IInstitutionalAccessService a, CancellationToken ct)
    {
        var grand = await Grand(h, db, a, "view", ct);
        if (!grand && (organizationId == null || !a.CanReadLodgeHospitalaria(h.User, organizationId.Value) ||
            !await DynamicHospitalariaAccess.AllowsAsync(db, h.User, organizationId.Value, "view", ct))) return Results.Forbid();
        if (month is < 1 or > 12 || year is < 1 or > 9999) return Results.BadRequest();
        await HospitalariaContributionGenerator.GenerateAsync(db, ct);
        h.Response.Headers.CacheControl = "private, no-store";
        var q = db.HospitalariaContributionObligations.AsNoTracking().Include(x => x.Organization).AsQueryable();
        if (organizationId != null) q = q.Where(x => x.OrganizationId == organizationId);
        if (year != null) q = q.Where(x => x.PeriodYear == year); if (month != null) q = q.Where(x => x.PeriodMonth == month);
        var items = await q.OrderByDescending(x => x.PeriodYear).ThenByDescending(x => x.PeriodMonth)
            .Select(x => new { x.Id, x.OrganizationId, taller = x.Organization.Name, x.RateId, x.PeriodYear, x.PeriodMonth,
                currency = "CLP", x.AmountDue, x.Status, x.PaymentDate, x.PaymentReference, x.ReviewedAtUtc, x.ReviewNotes }).ToListAsync(ct);
        return Results.Ok(new { total = items.Count, items });
    }
    private static async Task<IResult> PayAsync(Guid id, ContributionPaymentRequest r, HttpContext h, PmgmDbContext db, IInstitutionalAccessService a, IAuditService audit, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3546000)", ct);
        var row = await db.HospitalariaContributionObligations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row == null) return Results.NotFound();
        if (!a.CanManageLodgeHospitalaria(h.User, row.OrganizationId) || !await DynamicHospitalariaAccess.AllowsAsync(db, h.User, row.OrganizationId, "write", ct)) return Results.Forbid();
        if (row.Status is not ("pending" or "observed")) return Results.Conflict();
        if (r.Amount != row.AmountDue || r.PaymentDate > HospitalariaContributionGenerator.Today() || string.IsNullOrWhiteSpace(r.Reference) || r.Reference.Length > 500)
            return Results.BadRequest(new { message = "Pago completo, fecha no futura y comprobante obligatorio." });
        var previousReference = row.PaymentReference;
        row.Status = "submitted"; row.PaymentDate = r.PaymentDate; row.PaymentReference = r.Reference.Trim(); row.RecordedBySubject = Subject(h);
        row.ReviewedAtUtc = null; row.ReviewedBySubject = null; row.ReviewNotes = null;
        audit.Add(h, "hospitalaria.contribution.payment_submitted", nameof(HospitalariaContributionObligation), row.Id.ToString(), row.OrganizationId, AuditResults.Success,
            new { row.AmountDue, row.PaymentDate, row.PeriodYear, row.PeriodMonth, previousReference, row.PaymentReference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { row.Id, row.Status });
    }
    private static async Task<IResult> ReviewAsync(Guid id, ContributionReviewRequest r, HttpContext h, PmgmDbContext db, IInstitutionalAccessService a, IAuditService audit, CancellationToken ct)
    {
        if (!await Grand(h, db, a, "write", ct)) return Results.Forbid();
        if (r.Decision is not ("reconciled" or "observed") || (r.Decision == "observed" && string.IsNullOrWhiteSpace(r.Notes)) || r.Notes?.Length > 2000) return Results.BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3546000)", ct);
        var row = await db.HospitalariaContributionObligations.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row == null) return Results.NotFound(); if (row.Status != "submitted") return Results.Conflict();
        row.Status = r.Decision; row.ReviewedAtUtc = DateTimeOffset.UtcNow; row.ReviewedBySubject = Subject(h); row.ReviewNotes = r.Notes?.Trim();
        audit.Add(h, "hospitalaria.contribution.reviewed", nameof(HospitalariaContributionObligation), row.Id.ToString(), row.OrganizationId, AuditResults.Success,
            new { row.Status, row.PeriodYear, row.PeriodMonth });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(new { row.Id, row.Status });
    }
}
public sealed record ContributionRateRequest(decimal Amount, DateOnly EffectiveFrom);
public sealed record ContributionPaymentRequest(decimal Amount, DateOnly PaymentDate, string Reference);
public sealed record ContributionReviewRequest(string Decision, string? Notes);
