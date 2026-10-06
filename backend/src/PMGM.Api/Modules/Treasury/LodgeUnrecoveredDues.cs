using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static partial class LodgeTreasuryEndpoints
{
    private static async Task<IResult> GetLossCandidatesAsync(Guid organizationId, string? currencyCode, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId) ||
            !await DynamicTreasuryAccess.AllowsAsync(db, context.User, organizationId, "view", ct)) return Results.Forbid();
        var currency = TreasuryCurrency.Select(currencyCode, await TreasuryCurrency.ForOrganizationAsync(db, organizationId, ct));
        if (currency is null) return Results.BadRequest(new { message = "La moneda debe ser CLP o USD." });
        // A forced withdrawal is only a candidate. Its cause must be explicitly confirmed; do not parse private/free text.
        var withdrawals = await db.MemberWithdrawalRequests.AsNoTracking().Include(x => x.Member).ThenInclude(x => x.Person)
            .Where(x => x.OriginOrganizationId == organizationId && x.WithdrawalType == MembershipCodes.WithdrawalType.Forced &&
                x.Status == MembershipCodes.WithdrawalRequestStatus.Approved).OrderByDescending(x => x.RequestedEffectiveDate).ToListAsync(ct);
        var items = new List<object>();
        foreach (var withdrawal in withdrawals)
        {
            var charges = await LossCharges(db, organizationId, withdrawal.MemberId, currency, withdrawal.RequestedEffectiveDate).ToListAsync(ct);
            var amount = charges.Sum(x => Math.Max(0, x.MemberAmount - TotalPaid(x)));
            if (amount > 0) items.Add(new { withdrawalRequestId = withdrawal.Id, withdrawal.MemberId,
                memberDisplayName = $"{withdrawal.Member.Person.FirstNames} {withdrawal.Member.Person.LastNames}",
                withdrawalDate = withdrawal.RequestedEffectiveDate, currency, outstandingAmount = amount });
        }
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { items });
    }

    private static IQueryable<LodgeMemberCharge> LossCharges(PmgmDbContext db, Guid organizationId, Guid memberId, string currency, DateOnly withdrawalDate) =>
        db.LodgeMemberCharges.Include(x => x.Payments).Include(x => x.Allocations)
            .Where(x => x.OrganizationId == organizationId && x.MemberId == memberId && x.Currency == currency &&
                (x.PeriodYear < withdrawalDate.Year || (x.PeriodYear == withdrawalDate.Year && x.PeriodMonth <= withdrawalDate.Month)) &&
                !db.LodgeUnrecoveredDues.Any(loss => loss.ChargeId == x.Id));

    private static async Task<IResult> RecognizeUnrecoveredDuesAsync(Guid organizationId, RecognizeUnrecoveredDuesRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        try { return await RecognizeUnrecoveredDuesCoreAsync(organizationId, request, context, db, access, audit, ct); }
        catch (PostgresException e) when (e.SqlState is "40001" or "23505") { return LossConflict(); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "40001" or "23505" }) { return LossConflict(); }
        catch (InvalidOperationException e) when (e.InnerException is PostgresException { SqlState: "40001" or "23505" } or
            DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "23505" } }) { return LossConflict(); }
    }
    private static IResult LossConflict() => Results.Conflict(new { message = "La deuda cambió durante el registro. Actualice y revise el reporte antes de reintentar." });

    private static async Task<IResult> RecognizeUnrecoveredDuesCoreAsync(Guid organizationId, RecognizeUnrecoveredDuesRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId) ||
            !await DynamicTreasuryAccess.AllowsAsync(db, context.User, organizationId, "write", ct)) return Results.Forbid();
        var evidence = request.EvidenceReference?.Trim();
        if (!request.NonPaymentConfirmed || string.IsNullOrWhiteSpace(evidence) || evidence.Length > 500 ||
            request.RecognitionDate > GrandTreasuryTariff.Today())
            return Results.BadRequest(new { message = "Confirme la causa no pago, indique respaldo y una fecha de reconocimiento no futura." });
        var currency = TreasuryCurrency.Select(request.Currency, await TreasuryCurrency.ForOrganizationAsync(db, organizationId, ct));
        if (currency is null) return Results.BadRequest(new { message = "La moneda debe ser CLP o USD." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var withdrawal = await db.MemberWithdrawalRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.WithdrawalRequestId &&
            x.OriginOrganizationId == organizationId && x.WithdrawalType == MembershipCodes.WithdrawalType.Forced &&
            x.Status == MembershipCodes.WithdrawalRequestStatus.Approved, ct);
        if (withdrawal is null) return Results.Conflict(new { message = "Se requiere un retiro forzoso formal aprobado de este Taller." });
        if (request.RecognitionDate < withdrawal.RequestedEffectiveDate ||
            await IsAccountingYearClosedAsync(db, organizationId, request.RecognitionDate.Year, currency, ct))
            return Results.Conflict(new { message = "La fecha debe ser posterior o igual al retiro y pertenecer a un ejercicio abierto." });
        // Historical snapshot uses today's settled debt. Backdating it would include payments received after the claimed date.
        if (request.RecognitionDate != GrandTreasuryTariff.Today())
            return Results.BadRequest(new { message = "Reconozca la pérdida con la fecha actual; la deuda histórica conserva sus períodos originales." });
        var existing = await db.LodgeUnrecoveredDues.AsNoTracking().Include(x => x.Charge).ThenInclude(x => x.Member).ThenInclude(x => x.Person)
            .Where(x => x.WithdrawalRequestId == withdrawal.Id && x.Currency == currency).ToListAsync(ct);
        if (existing.Count > 0) return Results.Ok(new { total = existing.Sum(x => x.Amount), items = existing.Select(LossDto), alreadyRecorded = true });
        var charges = await LossCharges(db, organizationId, withdrawal.MemberId, currency, withdrawal.RequestedEffectiveDate)
            .Include(x => x.Member).ThenInclude(x => x.Person).OrderBy(x => x.Id).ToListAsync(ct);
        // An unallocated receipt is real money already received; require its allocation first, never call it a loss.
        var receipts = await db.LodgeMemberReceipts.Include(x => x.Allocations).Include(x => x.Adjustments)
            .Where(x => x.OrganizationId == organizationId && x.MemberId == withdrawal.MemberId && x.Currency == currency).ToListAsync(ct);
        if (receipts.Any(x => x.Amount + x.Adjustments.Sum(a => a.CashAmount) - x.Allocations.Sum(a => a.Amount) > 0))
            return Results.Conflict(new { message = "Existe saldo a favor recibido. Revise su imputación antes de reconocer cuotas no recuperadas." });
        var rows = charges.Where(x => x.MemberAmount > TotalPaid(x)).Select(x => new LodgeUnrecoveredDue
        {
            OrganizationId = organizationId, WithdrawalRequestId = withdrawal.Id, Charge = x, ChargeId = x.Id,
            RecognitionDate = request.RecognitionDate, Currency = currency, ChargedAmount = x.MemberAmount,
            PaidAmount = TotalPaid(x), Amount = x.MemberAmount - TotalPaid(x), EvidenceReference = evidence,
            RecordedBySubject = context.User.FindFirstValue("sub") ?? "unknown"
        }).ToList();
        if (rows.Count == 0) return Results.Conflict(new { message = "No existen cuotas impagas sin pérdida previa hasta el retiro." });
        db.LodgeUnrecoveredDues.AddRange(rows);
        audit.Add(context, "lodge.treasury.unrecovered_dues.recorded", "MemberWithdrawalRequest", withdrawal.Id.ToString(), organizationId,
            AuditResults.Success, new { withdrawal.MemberId, currency, amount = rows.Sum(x => x.Amount), chargeIds = rows.Select(x => x.ChargeId), request.RecognitionDate });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/gestion-logial/tesoreria/talleres/{organizationId}/reportes", new { total = rows.Sum(x => x.Amount), items = rows.Select(LossDto), alreadyRecorded = false });
    }

    private static object LossDto(LodgeUnrecoveredDue x) => new { x.Id, x.OrganizationId, x.WithdrawalRequestId, x.ChargeId,
        x.Charge.MemberId, memberDisplayName = $"{x.Charge.Member.Person.FirstNames} {x.Charge.Member.Person.LastNames}",
        x.Charge.PeriodYear, x.Charge.PeriodMonth, x.RecognitionDate, x.Currency, x.ChargedAmount, x.PaidAmount, x.Amount,
        x.EvidenceReference, x.RecordedBySubject, x.RecordedAtUtc };
}

public sealed record RecognizeUnrecoveredDuesRequest(Guid WithdrawalRequestId, DateOnly RecognitionDate, string Currency,
    bool NonPaymentConfirmed, string EvidenceReference);
