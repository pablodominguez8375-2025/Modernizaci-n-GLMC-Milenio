using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Hospitalaria.Entities;
namespace PMGM.Api.Modules.Hospitalaria;

public static class HospitalariaContributionGenerator
{
    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
    public static async Task GenerateAsync(PmgmDbContext db, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3546000)", ct);
        var today = Today();
        var rates = await db.HospitalariaContributionRates.AsNoTracking().OrderBy(x => x.EffectiveFrom).ToListAsync(ct);
        if (rates.Count == 0) { await tx.CommitAsync(ct); return; }
        var workshops = await db.Organizations.AsNoTracking().Where(x => x.Type == "workshop").Select(x => new { x.Id, x.CreatedAtUtc }).ToListAsync(ct);
        var existing = await db.HospitalariaContributionObligations.AsNoTracking().Select(x => new { x.OrganizationId, x.PeriodYear, x.PeriodMonth }).ToListAsync(ct);
        var keys = existing.Select(x => (x.OrganizationId, x.PeriodYear, x.PeriodMonth)).ToHashSet();
        var first = rates[0].EffectiveFrom;
        for (var month = new DateOnly(first.Year, first.Month, 1); month <= today; month = month.AddMonths(1))
        {
            var cutoff = month.AddMonths(1).AddDays(-1); if (cutoff > today) cutoff = today;
            var rate = rates.LastOrDefault(x => x.EffectiveFrom <= cutoff && (x.EffectiveUntil == null || x.EffectiveUntil >= cutoff));
            if (rate == null) continue;
            // El corte mensual se rige por el día civil de Chile, no por UTC.
            // Entre 20:00/21:00 y medianoche en Chile, UTC ya es el día siguiente.
            foreach (var workshop in workshops.Where(x => DateOnly.FromDateTime(
                         TimeZoneInfo.ConvertTimeBySystemTimeZoneId(x.CreatedAtUtc, "America/Santiago").DateTime) <= cutoff))
            {
                if (!keys.Add((workshop.Id, month.Year, month.Month))) continue;
                var obligation = new HospitalariaContributionObligation { OrganizationId = workshop.Id, RateId = rate.Id,
                    PeriodYear = month.Year, PeriodMonth = month.Month, AmountDue = rate.Amount };
                db.HospitalariaContributionObligations.Add(obligation);
                db.AuditEvents.Add(new AuditEvent { Action = "hospitalaria.contribution.generated", EntityType = nameof(HospitalariaContributionObligation),
                    EntityId = obligation.Id.ToString(), OrganizationId = workshop.Id, ActorSubject = "system:hospitalaria-monthly", Result = "success",
                    CorrelationId = Guid.NewGuid().ToString(), MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { obligation.PeriodYear, obligation.PeriodMonth, obligation.AmountDue, obligation.RateId }) });
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
public sealed class HospitalariaContributionWorker(IServiceScopeFactory scopes, ILogger<HospitalariaContributionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await using var scope = scopes.CreateAsyncScope(); await HospitalariaContributionGenerator.GenerateAsync(scope.ServiceProvider.GetRequiredService<PmgmDbContext>(), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "No se pudo generar el aporte mensual Hospitalaria; se reintentará."); }
        }
    }
}
