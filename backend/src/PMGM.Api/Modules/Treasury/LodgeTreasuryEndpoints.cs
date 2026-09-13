using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static class LodgeTreasuryEndpoints
{
    public static IEndpointRouteBuilder MapLodgeTreasuryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial/tesoreria")
            .WithTags("Tesorería del Taller")
            .RequireAuthorization();

        group.MapPost("/talleres/{organizationId:guid}/planes-cuota", CreateFeePlanAsync);
        group.MapGet("/talleres/{organizationId:guid}/planes-cuota", GetFeePlansAsync);
        group.MapPost("/talleres/{organizationId:guid}/cargos/generar", GenerateChargesAsync);
        group.MapGet("/talleres/{organizationId:guid}/resumen", GetSummaryAsync);
        group.MapPost("/cargos/{chargeId:guid}/pagos", AddPaymentAsync);
        group.MapPost("/talleres/{organizationId:guid}/egresos", CreateExpenseAsync);
        group.MapPost("/egresos/{expenseId:guid}/aprobar", ApproveExpenseAsync);
        group.MapGet("/hermanos/{memberId:guid}/cartola", GetMemberStatementAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateFeePlanAsync(Guid organizationId, CreateLodgeFeePlanRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (!TreasuryCodes.LodgeFeeType.IsValid(request.FeeType) || request.MemberAmount < 0 || request.GrandTreasuryAmount < 0)
            return Results.BadRequest(new { message = "El tipo y los montos de cuota deben ser válidos." });
        if (request.GrandTreasuryAmount > request.MemberAmount)
            return Results.BadRequest(new { message = "El aporte a Gran Tesorería no puede superar el monto cobrado al hermano." });
        if (request.EffectiveUntil is not null && request.EffectiveUntil < request.EffectiveFrom)
            return Results.BadRequest(new { message = "La vigencia final no puede ser anterior a la inicial." });

        var overlap = await db.LodgeFeePlans.AnyAsync(x => x.OrganizationId == organizationId && x.FeeType == request.FeeType &&
            x.IsActive && (x.EffectiveUntil == null || x.EffectiveUntil >= request.EffectiveFrom) &&
            (request.EffectiveUntil == null || x.EffectiveFrom <= request.EffectiveUntil), cancellationToken);
        if (overlap) return Results.Conflict(new { message = "Ya existe una cuota activa del mismo tipo para esa vigencia." });

        var plan = new LodgeFeePlan { OrganizationId = organizationId, FeeType = request.FeeType,
            MemberAmount = request.MemberAmount, GrandTreasuryAmount = request.GrandTreasuryAmount,
            EffectiveFrom = request.EffectiveFrom, EffectiveUntil = request.EffectiveUntil };
        db.LodgeFeePlans.Add(plan);
        audit.Add(context, "lodge.treasury.fee_plan.created", nameof(LodgeFeePlan), plan.Id.ToString(),
            organizationId, AuditResults.Success,
            new { plan.FeeType, plan.MemberAmount, plan.GrandTreasuryAmount, plan.EffectiveFrom, plan.EffectiveUntil });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gestion-logial/tesoreria/talleres/{organizationId}/planes-cuota/{plan.Id}", ToFeePlan(plan));
    }

    private static async Task<IResult> GetFeePlansAsync(Guid organizationId, DateOnly? asOf, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        var date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await db.LodgeFeePlans.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.IsActive &&
            x.EffectiveFrom <= date && (x.EffectiveUntil == null || x.EffectiveUntil >= date))
            .OrderBy(x => x.FeeType).ToListAsync(cancellationToken);
        var items = rows.Select(ToFeePlan).ToList();
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> GenerateChargesAsync(Guid organizationId, GenerateLodgeChargesRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (request.PeriodMonth is < 1 or > 12 || request.PeriodYear is < 2000 or > 2200)
            return Results.BadRequest(new { message = "El período indicado no es válido." });
        var cutoff = new DateOnly(request.PeriodYear, request.PeriodMonth, DateTime.DaysInMonth(request.PeriodYear, request.PeriodMonth));
        var plans = await db.LodgeFeePlans.Where(x => x.OrganizationId == organizationId && x.IsActive && x.EffectiveFrom <= cutoff &&
            (x.EffectiveUntil == null || x.EffectiveUntil >= new DateOnly(request.PeriodYear, request.PeriodMonth, 1)))
            .ToDictionaryAsync(x => x.FeeType, cancellationToken);
        if (!plans.ContainsKey(TreasuryCodes.LodgeFeeType.Normal))
            return Results.Conflict(new { message = "Debe configurar al menos la cuota normal vigente." });

        var existing = await db.LodgeMemberCharges.Where(x => x.OrganizationId == organizationId &&
            x.PeriodYear == request.PeriodYear && x.PeriodMonth == request.PeriodMonth).Select(x => x.MemberId).ToListAsync(cancellationToken);
        var members = await db.Memberships.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.Status == MembershipCodes.MembershipStatus.Active &&
            x.StartDate <= cutoff && (x.EndDate == null || x.EndDate >= cutoff) && !existing.Contains(x.MemberId))
            .Select(x => x.MemberId).Distinct().ToListAsync(cancellationToken);
        var assignments = request.Assignments?.ToDictionary(x => x.MemberId, x => x.FeeType) ?? new Dictionary<Guid, string>();
        var created = 0;
        foreach (var memberId in members)
        {
            var feeType = assignments.GetValueOrDefault(memberId, TreasuryCodes.LodgeFeeType.Normal);
            if (!TreasuryCodes.LodgeFeeType.IsValid(feeType) || !plans.TryGetValue(feeType, out var plan))
                return Results.BadRequest(new { message = $"No existe una cuota vigente para el tipo '{feeType}'.", memberId });
            db.LodgeMemberCharges.Add(new LodgeMemberCharge { OrganizationId = organizationId, MemberId = memberId,
                FeePlanId = plan.Id, PeriodYear = request.PeriodYear, PeriodMonth = request.PeriodMonth,
                MemberAmount = plan.MemberAmount, GrandTreasuryAmount = plan.GrandTreasuryAmount,
                Status = TreasuryCodes.LodgeChargeStatus.Pending });
            created++;
        }
        audit.Add(context, "lodge.treasury.charges.generated", nameof(LodgeMemberCharge), $"{organizationId}:{request.PeriodYear}-{request.PeriodMonth:00}",
            organizationId, AuditResults.Success, new { request.PeriodYear, request.PeriodMonth, created });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildSummaryAsync(db, organizationId, request.PeriodYear, request.PeriodMonth, cancellationToken));
    }

    private static async Task<IResult> AddPaymentAsync(Guid chargeId, AddLodgeMemberPaymentRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        var charge = await db.LodgeMemberCharges.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == chargeId, cancellationToken);
        if (charge is null) return Results.NotFound(new { message = "El cargo indicado no existe." });
        if (!access.CanManageLodgeTreasury(context.User, charge.OrganizationId)) return Results.Forbid();
        if (request.Amount <= 0 || !TreasuryCodes.LodgePaymentMethod.IsValid(request.PaymentMethod))
            return Results.BadRequest(new { message = "El monto y medio de pago deben ser válidos." });
        var paid = charge.Payments.Sum(x => x.Amount);
        if (paid + request.Amount > charge.MemberAmount)
            return Results.Conflict(new { message = "El abono supera el saldo pendiente del hermano." });

        var payment = new LodgeMemberPayment { ChargeId = charge.Id, Amount = request.Amount, PaymentMethod = request.PaymentMethod,
            PaymentDate = request.PaymentDate, ReceiptNumber = $"REC-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            Reference = Normalize(request.Reference), RecordedBySubject = context.User.FindFirstValue("sub") ?? "unknown" };
        db.LodgeMemberPayments.Add(payment);
        charge.Status = paid + request.Amount == charge.MemberAmount ? TreasuryCodes.LodgeChargeStatus.Paid : TreasuryCodes.LodgeChargeStatus.Partial;
        audit.Add(context, "lodge.treasury.member_payment.recorded", nameof(LodgeMemberPayment), payment.Id.ToString(),
            charge.OrganizationId, AuditResults.Success,
            new { charge.Id, payment.Amount, payment.PaymentMethod, payment.ReceiptNumber });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gestion-logial/tesoreria/cargos/{chargeId}/pagos/{payment.Id}",
            new { payment.Id, payment.ReceiptNumber, payment.Amount, payment.PaymentMethod, payment.PaymentDate,
                paidAmount = paid + payment.Amount, balance = charge.MemberAmount - paid - payment.Amount, charge.Status });
    }

    private static async Task<IResult> GetSummaryAsync(Guid organizationId, int year, int month, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        return Results.Ok(await BuildSummaryAsync(db, organizationId, year, month, cancellationToken));
    }

    private static async Task<IResult> GetMemberStatementAsync(Guid memberId, Guid organizationId, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        var charges = await db.LodgeMemberCharges.AsNoTracking().Include(x => x.Payments)
            .Where(x => x.OrganizationId == organizationId && x.MemberId == memberId)
            .OrderByDescending(x => x.PeriodYear).ThenByDescending(x => x.PeriodMonth).ToListAsync(cancellationToken);
        return Results.Ok(new { memberId, organizationId, totalCharged = charges.Sum(x => x.MemberAmount),
            totalPaid = charges.Sum(x => x.Payments.Sum(p => p.Amount)),
            balance = charges.Sum(x => x.MemberAmount - x.Payments.Sum(p => p.Amount)),
            items = charges.Select(x => new { x.Id, x.PeriodYear, x.PeriodMonth, x.MemberAmount, x.GrandTreasuryAmount,
                paidAmount = x.Payments.Sum(p => p.Amount), balance = x.MemberAmount - x.Payments.Sum(p => p.Amount), x.Status,
                payments = x.Payments.OrderByDescending(p => p.PaymentDate).Select(p => new { p.Id, p.ReceiptNumber, p.Amount, p.PaymentMethod, p.PaymentDate, p.Reference }) }) });
    }

    private static async Task<object> BuildSummaryAsync(PmgmDbContext db, Guid organizationId, int year, int month, CancellationToken cancellationToken)
    {
        var charges = await db.LodgeMemberCharges.AsNoTracking().Include(x => x.Payments)
            .Where(x => x.OrganizationId == organizationId && x.PeriodYear == year && x.PeriodMonth == month).ToListAsync(cancellationToken);
        var memberExpected = charges.Sum(x => x.MemberAmount);
        var collected = charges.Sum(x => x.Payments.Sum(p => p.Amount));
        var grandTreasuryExpected = charges.Sum(x => x.GrandTreasuryAmount);
        return new { organizationId, periodYear = year, periodMonth = month, members = charges.Count,
            memberExpected, collected, receivable = memberExpected - collected, grandTreasuryExpected,
            workshopMarginProjected = memberExpected - grandTreasuryExpected,
            paid = charges.Count(x => x.Status == TreasuryCodes.LodgeChargeStatus.Paid),
            partial = charges.Count(x => x.Status == TreasuryCodes.LodgeChargeStatus.Partial),
            overdue = charges.Count(x => x.Status == TreasuryCodes.LodgeChargeStatus.Pending),
            trafficLight = charges.Count == 0 ? "no_data" : collected >= memberExpected ? "green" : collected >= memberExpected * .8m ? "amber" : "red" };
    }

    private static object ToFeePlan(LodgeFeePlan x) => new { x.Id, x.OrganizationId, x.FeeType, x.MemberAmount,
        x.GrandTreasuryAmount, workshopAmount = x.MemberAmount - x.GrandTreasuryAmount, x.EffectiveFrom, x.EffectiveUntil, x.IsActive };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<IResult> CreateExpenseAsync(Guid organizationId, CreateLodgeTreasuryExpenseRequest request, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Description)) return Results.BadRequest(new { message = "Categoría, descripción y monto son obligatorios." });
        var expense = new LodgeTreasuryExpense { OrganizationId = organizationId, Category = request.Category.Trim(), Amount = request.Amount, ExpenseDate = request.ExpenseDate, Description = request.Description.Trim(), EvidenceReference = Normalize(request.EvidenceReference), ApprovalStatus = "pending_approval", RecordedBySubject = context.User.FindFirstValue("sub") ?? "unknown" };
        db.LodgeTreasuryExpenses.Add(expense); audit.Add(context, "lodge.treasury.expense.recorded", nameof(LodgeTreasuryExpense), expense.Id.ToString(), organizationId, AuditResults.Success, new { expense.Category, expense.Amount }); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/gestion-logial/tesoreria/egresos/{expense.Id}", expense);
    }

    private static async Task<IResult> ApproveExpenseAsync(Guid expenseId, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var expense = await db.LodgeTreasuryExpenses.SingleOrDefaultAsync(x => x.Id == expenseId, ct); if (expense is null) return Results.NotFound();
        if (!access.CanApproveLodgeExpenses(context.User, expense.OrganizationId)) return Results.Forbid();
        if (expense.ApprovalStatus != "pending_approval") return Results.Conflict(new { message = "El egreso ya fue resuelto." });
        expense.ApprovalStatus = "approved"; expense.ApprovedBySubject = context.User.FindFirstValue("sub") ?? "unknown"; expense.ApprovedAtUtc = DateTimeOffset.UtcNow;
        audit.Add(context, "lodge.treasury.expense.approved", nameof(LodgeTreasuryExpense), expense.Id.ToString(), expense.OrganizationId, AuditResults.Success, new { expense.Category, expense.Amount }); await db.SaveChangesAsync(ct); return Results.Ok(expense);
    }
}

public sealed record CreateLodgeFeePlanRequest(string FeeType, decimal MemberAmount, decimal GrandTreasuryAmount,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil);
public sealed record LodgeFeeAssignmentRequest(Guid MemberId, string FeeType);
public sealed record GenerateLodgeChargesRequest(int PeriodYear, int PeriodMonth, IReadOnlyList<LodgeFeeAssignmentRequest>? Assignments);
public sealed record AddLodgeMemberPaymentRequest(decimal Amount, string PaymentMethod, DateOnly PaymentDate, string? Reference);
public sealed record CreateLodgeTreasuryExpenseRequest(string Category, decimal Amount, DateOnly ExpenseDate, string Description, string? EvidenceReference);
