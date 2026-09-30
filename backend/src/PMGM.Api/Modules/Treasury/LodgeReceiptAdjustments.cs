using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static partial class LodgeTreasuryEndpoints
{
    private static IQueryable<LodgeReceiptAdjustment> CashAdjustments(PmgmDbContext db, Guid organizationId, string currency) =>
        db.LodgeReceiptAdjustments.AsNoTracking().Where(x => x.Receipt.OrganizationId == organizationId && x.Receipt.Currency == currency && x.Kind == "void");

    private static async Task<IResult> AdjustReceiptAsync(Guid receiptId, AdjustLodgeReceiptRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        try { return await AdjustReceiptCoreAsync(receiptId, request, context, db, access, audit, ct); }
        catch (PostgresException e) when (e.SqlState is "40001" or "23505")
        { return Results.Conflict(new { message = "El recibo cambió durante el registro. Actualice y reintente con la misma clave." }); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "40001" or "23505" })
        { return Results.Conflict(new { message = "El recibo cambió durante el registro. Actualice y reintente con la misma clave." }); }
        // Npgsql's non-retrying execution strategy wraps transient PostgreSQL failures.
        catch (InvalidOperationException e) when (
            e.InnerException is PostgresException { SqlState: "40001" or "23505" } or
                DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "23505" } })
        { return Results.Conflict(new { message = "El recibo cambió durante el registro. Actualice y reintente con la misma clave." }); }
    }

    private static async Task<IResult> AdjustReceiptCoreAsync(Guid receiptId, AdjustLodgeReceiptRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var receipt = await db.LodgeMemberReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receiptId, ct);
        if (receipt is null) return Results.NotFound();
        if (!access.CanManageLodgeTreasury(context.User, receipt.OrganizationId)) return Results.Forbid();
        var reason = request.Reason?.Trim();
        var key = request.IdempotencyKey?.Trim();
        if (request.Kind is not ("void" or "correction") || string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 ||
            string.IsNullOrWhiteSpace(key) || key.Length > 100 || request.Allocations is null ||
            request.Allocations.Any(x => x.Amount <= 0) || request.Allocations.Select(x => x.ChargeId).Distinct().Count() != request.Allocations.Count ||
            request.EffectiveDate < receipt.PaymentDate || request.EffectiveDate.Year is < 2000 or > 2200)
            return Results.BadRequest(new { message = "Indique tipo, fecha, motivo, clave e imputaciones válidos." });
        var payload = JsonSerializer.Serialize(new { request.Kind, request.EffectiveDate, reason, request.AllocationId, request.Amount,
            allocations = request.Allocations.OrderBy(x => x.ChargeId).ToArray() });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        receipt = await db.LodgeMemberReceipts.Include(x => x.Allocations).ThenInclude(x => x.Charge)
            .Include(x => x.Adjustments).SingleAsync(x => x.Id == receiptId, ct);
        var replay = receipt.Adjustments.SingleOrDefault(x => x.IdempotencyKey == key);
        if (replay is not null)
            return replay.RequestPayload == payload ? Results.Ok(AdjustmentResult(receipt, replay)) : Results.Conflict(new { message = "La clave ya fue utilizada con datos distintos." });
        if (receipt.Adjustments.Any(x => x.Kind == "void")) return Results.Conflict(new { message = "El recibo ya está anulado." });
        if (await IsAccountingYearClosedAsync(db, receipt.OrganizationId, request.EffectiveDate.Year, receipt.Currency, ct))
            return Results.Conflict(new { message = "El ajuste requiere un ejercicio abierto; no se reabre el original." });
        if (receipt.Allocations.Any(x => (x.EffectiveDate ?? receipt.PaymentDate) > request.EffectiveDate) ||
            receipt.Adjustments.Any(x => x.EffectiveDate > request.EffectiveDate))
            return Results.Conflict(new { message = "La fecha del ajuste no puede preceder las imputaciones o ajustes existentes." });
        var adjustment = new LodgeReceiptAdjustment { ReceiptId = receipt.Id, Receipt = receipt, Kind = request.Kind,
            EffectiveDate = request.EffectiveDate, CashAmount = request.Kind == "void" ? -receipt.Amount : 0,
            Reason = reason, IdempotencyKey = key, RequestPayload = payload,
            RecordedBySubject = context.User.FindFirstValue("sub") ?? "unknown" };
        var entries = new List<LodgeMemberPaymentAllocation>();
        decimal Remaining(LodgeMemberPaymentAllocation source) => source.Amount + receipt.Allocations.Where(x => x.ReversesAllocationId == source.Id).Sum(x => x.Amount);
        void Reverse(LodgeMemberPaymentAllocation source, decimal amount) => entries.Add(new LodgeMemberPaymentAllocation {
            ReceiptId = receipt.Id, ChargeId = source.ChargeId, Amount = -amount, ReversesAllocationId = source.Id,
            AdjustmentId = adjustment.Id, EffectiveDate = request.EffectiveDate, AllocatedBySubject = adjustment.RecordedBySubject });
        if (request.Kind == "void")
        {
            if (request.Allocations.Count != 0 || request.AllocationId is not null || request.Amount is not null)
                return Results.BadRequest(new { message = "La anulación del registro es total; no es una devolución." });
            foreach (var source in receipt.Allocations.Where(x => x.Amount > 0))
            {
                var remaining = Remaining(source);
                if (remaining > 0) Reverse(source, remaining);
            }
        }
        else
        {
            var source = receipt.Allocations.SingleOrDefault(x => x.Id == request.AllocationId && x.Amount > 0);
            if (source is null || request.Amount is null || request.Amount <= 0 || request.Amount > Remaining(source) || request.Allocations.Sum(x => x.Amount) > request.Amount)
                return Results.Conflict(new { message = "La corrección supera la imputación disponible o no identifica su origen." });
            Reverse(source, request.Amount.Value);
            var ids = request.Allocations.Select(x => x.ChargeId).ToArray();
            var charges = await db.LodgeMemberCharges.Include(x => x.Payments).Include(x => x.Allocations).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            if (charges.Count != ids.Length || charges.Values.Any(x => x.OrganizationId != receipt.OrganizationId || x.MemberId != receipt.MemberId || x.Currency != receipt.Currency))
                return Results.BadRequest(new { message = "Los destinos deben pertenecer al mismo hermano, Taller y moneda." });
            foreach (var item in request.Allocations)
            {
                var charge = charges[item.ChargeId];
                var paid = TotalPaid(charge) - (charge.Id == source.ChargeId ? request.Amount.Value : 0);
                if (paid + item.Amount > charge.MemberAmount)
                    return Results.Conflict(new { message = "La corrección supera el saldo del destino." });
                entries.Add(new LodgeMemberPaymentAllocation { ReceiptId = receipt.Id, ChargeId = charge.Id, Amount = item.Amount,
                    AdjustmentId = adjustment.Id, EffectiveDate = request.EffectiveDate, AllocatedBySubject = adjustment.RecordedBySubject });
            }
        }
        db.LodgeReceiptAdjustments.Add(adjustment);
        db.LodgeMemberPaymentAllocations.AddRange(entries);
        audit.Add(context, "lodge.treasury.member_receipt.adjusted", nameof(LodgeReceiptAdjustment), adjustment.Id.ToString(), receipt.OrganizationId,
            AuditResults.Success, new { receipt.Id, receipt.ReceiptNumber, adjustment.Kind, adjustment.EffectiveDate, adjustment.Reason, adjustment.CashAmount });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        // Reload to return the committed net state rather than mutating original entities.
        db.ChangeTracker.Clear();
        receipt = await db.LodgeMemberReceipts.AsNoTracking().Include(x => x.Allocations).Include(x => x.Adjustments).SingleAsync(x => x.Id == receiptId, ct);
        return Results.Created($"/api/gestion-logial/tesoreria/recibos/{receiptId}/ajustes/{adjustment.Id}", AdjustmentResult(receipt, adjustment));
    }

    private static object AdjustmentResult(LodgeMemberReceipt receipt, LodgeReceiptAdjustment adjustment) => new {
        adjustment.Id, receiptId = receipt.Id, receipt.ReceiptNumber, adjustment.Kind, adjustment.EffectiveDate,
        adjustment.Reason, adjustment.CashAmount, adjustment.RecordedBySubject, adjustment.RecordedAtUtc,
        allocatedAmount = receipt.Allocations.Sum(x => x.Amount),
        unappliedBalance = receipt.Amount + receipt.Adjustments.Sum(x => x.CashAmount) - receipt.Allocations.Sum(x => x.Amount)
    };
}

public sealed record AdjustLodgeReceiptRequest(string Kind, DateOnly EffectiveDate, string Reason, string IdempotencyKey,
    IReadOnlyList<LodgeReceiptAllocationRequest> Allocations, Guid? AllocationId = null, decimal? Amount = null);
