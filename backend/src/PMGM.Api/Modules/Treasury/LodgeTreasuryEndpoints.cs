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
        group.MapGet("/talleres/{organizationId:guid}/cargos", GetChargesAsync);
        group.MapGet("/talleres/{organizationId:guid}/resumen", GetSummaryAsync);
        group.MapGet("/talleres/{organizationId:guid}/caja", GetCashSummaryAsync);
        group.MapGet("/talleres/{organizationId:guid}/reportes", GetCashReportAsync);
        group.MapPost("/talleres/{organizationId:guid}/ingresos", CreateIncomeAsync);
        group.MapGet("/talleres/{organizationId:guid}/configuracion", GetConfigurationAsync);
        group.MapPut("/talleres/{organizationId:guid}/configuracion", SaveConfigurationAsync);
        group.MapGet("/talleres/{organizationId:guid}/cierres-anuales", GetAnnualClosuresAsync);
        group.MapPost("/talleres/{organizationId:guid}/cierres-anuales/{year:int}/cerrar", CloseAnnualPeriodAsync);
        group.MapPost("/cargos/{chargeId:guid}/pagos", AddPaymentAsync);
        group.MapPost("/talleres/{organizationId:guid}/egresos", CreateExpenseAsync);
        group.MapGet("/talleres/{organizationId:guid}/egresos", GetExpensesAsync);
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
        if (request.EffectiveUntil is not null && request.EffectiveUntil < request.EffectiveFrom)
            return Results.BadRequest(new { message = "La vigencia final no puede ser anterior a la inicial." });

        var organization = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        if (organization is null) return Results.NotFound(new { message = "El Taller no existe." });
        if (organization.TreasuryTerritory is null)
            return Results.Conflict(new { message = "Gran Tesorería debe clasificar el Oriente del Taller antes de configurar cuotas." });
        var officialAmount = GrandTreasuryFeeSchedule.Resolve(request.FeeType, organization.TreasuryTerritory, request.EffectiveFrom);
        if (officialAmount is null)
            return Results.Conflict(new { message = "No existe una tarifa institucional CLP aplicable. Verifique vigencia, Oriente y moneda del decreto." });
        if (request.MemberAmount < officialAmount.Value.Amount)
            return Results.BadRequest(new { message = "La cuota local no puede ser inferior al aporte decretado a Gran Tesorería." });
        if (request.GrandTreasuryAmount != officialAmount.Value.Amount)
            return Results.BadRequest(new { message = $"Gran Tesorería fija este aporte en {officialAmount.Value.Amount} {officialAmount.Value.Currency}; el Taller sólo define el monto local." });

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
        var territory = await db.Organizations.AsNoTracking().Where(x => x.Id == organizationId)
            .Select(x => x.TreasuryTerritory).SingleOrDefaultAsync(cancellationToken);
        var items = rows.Select(x => ToFeePlan(x, territory)).ToList();
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> GenerateChargesAsync(Guid organizationId, GenerateLodgeChargesRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (request.PeriodMonth is < 1 or > 12 || request.PeriodYear is < 2000 or > 2200)
            return Results.BadRequest(new { message = "El período indicado no es válido." });
        if (await IsAccountingYearClosedAsync(db, organizationId, request.PeriodYear, cancellationToken))
            return Results.Conflict(new { message = "El ejercicio contable está cerrado y no admite nuevos cargos." });
        var cutoff = new DateOnly(request.PeriodYear, request.PeriodMonth, DateTime.DaysInMonth(request.PeriodYear, request.PeriodMonth));
        var territory = await db.Organizations.AsNoTracking().Where(x => x.Id == organizationId)
            .Select(x => x.TreasuryTerritory).SingleOrDefaultAsync(cancellationToken);
        if (territory is null) return Results.Conflict(new { message = "Gran Tesorería debe clasificar el Oriente del Taller antes de generar cargos." });
        var plans = await db.LodgeFeePlans.Where(x => x.OrganizationId == organizationId && x.IsActive && x.EffectiveFrom <= cutoff &&
            (x.EffectiveUntil == null || x.EffectiveUntil >= new DateOnly(request.PeriodYear, request.PeriodMonth, 1)))
            .ToDictionaryAsync(x => x.FeeType, cancellationToken);
        if (!plans.ContainsKey(TreasuryCodes.LodgeFeeType.Normal))
            return Results.Conflict(new { message = "Debe configurar al menos la cuota normal vigente." });

        var existing = await db.LodgeMemberCharges.Where(x => x.OrganizationId == organizationId &&
            x.PeriodYear == request.PeriodYear && x.PeriodMonth == request.PeriodMonth).Select(x => x.MemberId).ToListAsync(cancellationToken);
        var members = await db.Memberships.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.Status == MembershipCodes.MembershipStatus.Active &&
            x.StartDate <= cutoff && (x.EndDate == null || x.EndDate >= cutoff) && !existing.Contains(x.MemberId))
            .Select(x => new { x.MemberId, x.MembershipType }).Distinct().ToListAsync(cancellationToken);
        var assignments = request.Assignments?.ToDictionary(x => x.MemberId, x => x.FeeType) ?? new Dictionary<Guid, string>();
        var created = 0;
        foreach (var membership in members)
        {
            var feeType = assignments.GetValueOrDefault(membership.MemberId,
                membership.MembershipType == GrandTreasuryFeeSchedule.PastActiveMembershipType
                    ? TreasuryCodes.LodgeFeeType.PastActive
                    : TreasuryCodes.LodgeFeeType.Normal);
            if (!TreasuryCodes.LodgeFeeType.IsValid(feeType))
                return Results.BadRequest(new { message = $"El tipo de cuota '{feeType}' no es válido.", memberId = membership.MemberId });
            if (!plans.TryGetValue(feeType, out var plan))
            {
                if (feeType == TreasuryCodes.LodgeFeeType.PastActive) continue;
                return Results.BadRequest(new { message = $"No existe una cuota vigente para el tipo '{feeType}'.", memberId = membership.MemberId });
            }
            var officialAmount = GrandTreasuryFeeSchedule.Resolve(feeType, territory, cutoff);
            if (officialAmount is null)
                return Results.Conflict(new { message = "No existe una tarifa institucional aplicable para esta moneda, categoría y vigencia.", memberId = membership.MemberId, feeType, territory });
            if (plan.MemberAmount < officialAmount.Value.Amount)
                return Results.Conflict(new { message = "La cuota local vigente quedó bajo el aporte decretado a Gran Tesorería.", memberId = membership.MemberId, feeType });
            db.LodgeMemberCharges.Add(new LodgeMemberCharge { OrganizationId = organizationId, MemberId = membership.MemberId,
                FeePlanId = plan.Id, PeriodYear = request.PeriodYear, PeriodMonth = request.PeriodMonth,
                MemberAmount = plan.MemberAmount, GrandTreasuryAmount = officialAmount.Value.Amount,
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
        var key = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            return Results.BadRequest(new { message = "La clave de registro del pago no es válida." });
        var reference = Normalize(request.Reference);
        var existing = charge.Payments.FirstOrDefault(x => x.IdempotencyKey == key);
        if (existing is not null)
        {
            if (existing.Amount != request.Amount || existing.PaymentMethod != request.PaymentMethod ||
                existing.PaymentDate != request.PaymentDate || !string.Equals(existing.Reference, reference, StringComparison.Ordinal))
                return Results.Conflict(new { message = "La clave ya fue utilizada con datos de pago distintos." });
            var alreadyPaid = charge.Payments.Where(x => x.Id != existing.Id).Sum(x => x.Amount) + existing.Amount;
            return Results.Ok(new { existing.Id, existing.ReceiptNumber, existing.Amount, existing.PaymentMethod, existing.PaymentDate,
                paidAmount = alreadyPaid, balance = charge.MemberAmount - alreadyPaid, charge.Status });
        }
        if (await IsAccountingYearClosedAsync(db, charge.OrganizationId, request.PaymentDate.Year, cancellationToken))
            return Results.Conflict(new { message = "La fecha de pago pertenece a un ejercicio cerrado. Registre el movimiento en un período abierto." });
        var paid = charge.Payments.Sum(x => x.Amount);
        if (paid + request.Amount > charge.MemberAmount)
            return Results.Conflict(new { message = "El abono supera el saldo pendiente del hermano." });
        if (reference is not null && charge.Payments.Any(x => x.Amount == request.Amount && x.PaymentDate == request.PaymentDate &&
                x.PaymentMethod == request.PaymentMethod && string.Equals(x.Reference, reference, StringComparison.OrdinalIgnoreCase)))
            return Results.Conflict(new { message = "Este pago ya fue registrado para el mismo cargo, fecha, monto, medio y referencia." });

        var payment = new LodgeMemberPayment { ChargeId = charge.Id, Amount = request.Amount, PaymentMethod = request.PaymentMethod,
            PaymentDate = request.PaymentDate, IdempotencyKey = key,
            ReceiptNumber = $"REC-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            Reference = reference, RecordedBySubject = context.User.FindFirstValue("sub") ?? "unknown" };
        db.LodgeMemberPayments.Add(payment);
        charge.Status = paid + request.Amount == charge.MemberAmount ? TreasuryCodes.LodgeChargeStatus.Paid : TreasuryCodes.LodgeChargeStatus.Partial;
        audit.Add(context, "lodge.treasury.member_payment.recorded", nameof(LodgeMemberPayment), payment.Id.ToString(),
            charge.OrganizationId, AuditResults.Success, new { charge.Id, payment.Amount, payment.PaymentMethod, payment.ReceiptNumber });
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

    private static async Task<IResult> GetChargesAsync(Guid organizationId, int year, int month, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (month is < 1 or > 12 || year is < 2000 or > 2200)
            return Results.BadRequest(new { message = "El período indicado no es válido." });
        var cutoff = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var rows = await db.LodgeMemberCharges.AsNoTracking().Include(x => x.Payments.Where(p => p.PaymentDate <= cutoff))
            .Where(x => x.OrganizationId == organizationId && (x.PeriodYear < year || (x.PeriodYear == year && x.PeriodMonth <= month)))
            .Join(db.Members.AsNoTracking().Include(x => x.Person), charge => charge.MemberId, member => member.Id,
                (charge, member) => new { charge, member })
            .OrderBy(x => x.member.Person.LastNames).ThenBy(x => x.member.Person.FirstNames)
            .ToListAsync(cancellationToken);
        var items = rows.GroupBy(x => new { x.charge.MemberId, x.member.Person.FirstNames, x.member.Person.LastNames })
            .Select(group =>
            {
                var memberCharges = group.Select(x => x.charge).OrderBy(x => x.PeriodYear).ThenBy(x => x.PeriodMonth).ToList();
                var totalPaid = memberCharges.Sum(x => x.Payments.Sum(p => p.Amount));
                var totalBalance = memberCharges.Sum(x => x.MemberAmount - x.Payments.Sum(p => p.Amount));
                var nextDueCharge = memberCharges.FirstOrDefault(x => x.MemberAmount > x.Payments.Sum(p => p.Amount)) ?? memberCharges[^1];
                var currentCharge = memberCharges[^1];
                return new { id = nextDueCharge.Id, group.Key.MemberId,
                    memberDisplayName = group.Key.FirstNames + " " + group.Key.LastNames,
                    memberAmount = memberCharges.Sum(x => x.MemberAmount), monthlyFeeAmount = currentCharge.MemberAmount,
                    paidAmount = totalPaid, balance = totalBalance,
                    maxPaymentAmount = nextDueCharge.MemberAmount - nextDueCharge.Payments.Sum(p => p.Amount),
                    status = totalBalance <= 0 ? TreasuryCodes.LodgeChargeStatus.Paid : totalPaid > 0 ? TreasuryCodes.LodgeChargeStatus.Partial : TreasuryCodes.LodgeChargeStatus.Pending,
                    payments = memberCharges.SelectMany(x => x.Payments).OrderByDescending(p => p.PaymentDate).Select(p => new
                        { p.Id, p.ReceiptNumber, p.Amount, p.PaymentMethod, p.PaymentDate, p.Reference }) };
            }).ToList();
        return Results.Ok(new { total = items.Count, items });
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

    private static async Task<IResult> CreateIncomeAsync(Guid organizationId, CreateLodgeTreasuryIncomeRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Description))
            return Results.BadRequest(new { message = "Categoría, descripción y monto son obligatorios." });
        if (await IsAccountingYearClosedAsync(db, organizationId, request.IncomeDate.Year, ct))
            return Results.Conflict(new { message = "La fecha de ingreso pertenece a un ejercicio cerrado. Registre el movimiento en un período abierto." });
        var income = new LodgeTreasuryIncome { OrganizationId = organizationId, Category = request.Category.Trim(), Amount = request.Amount,
            IncomeDate = request.IncomeDate, Description = request.Description.Trim(), EvidenceReference = Normalize(request.EvidenceReference),
            RecordedBySubject = context.User.FindFirstValue("sub") ?? "unknown" };
        db.LodgeTreasuryIncomes.Add(income);
        audit.Add(context, "lodge.treasury.income.recorded", nameof(LodgeTreasuryIncome), income.Id.ToString(), organizationId,
            AuditResults.Success, new { income.Category, income.Amount, income.IncomeDate });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/gestion-logial/tesoreria/ingresos/{income.Id}", income);
    }

    private static async Task<IResult> GetCashSummaryAsync(Guid organizationId, DateOnly? asOf, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        var cutoff = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(cutoff.Year, cutoff.Month, 1);
        var config = await db.LodgeTreasuryConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
        var openingDate = config?.OpeningBalanceDate ?? DateOnly.MinValue;
        var openingBalance = config is not null && config.OpeningBalanceDate <= cutoff ? config.OpeningBalance : 0m;
        var payments = await db.LodgeMemberPayments.AsNoTracking().Where(x => x.Charge.OrganizationId == organizationId && x.PaymentDate >= openingDate && x.PaymentDate <= cutoff).ToListAsync(ct);
        var incomes = await db.LodgeTreasuryIncomes.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.IncomeDate >= openingDate && x.IncomeDate <= cutoff).ToListAsync(ct);
        var expenses = await db.LodgeTreasuryExpenses.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.ExpenseDate >= openingDate && x.ExpenseDate <= cutoff && x.ApprovalStatus == "approved").ToListAsync(ct);
        var monthPayments = payments.Where(x => x.PaymentDate >= monthStart).Sum(x => x.Amount);
        var monthIncomes = incomes.Where(x => x.IncomeDate >= monthStart).Sum(x => x.Amount);
        var monthExpenses = expenses.Where(x => x.ExpenseDate >= monthStart).Sum(x => x.Amount);
        var allIncome = payments.Sum(x => x.Amount) + incomes.Sum(x => x.Amount);
        var allExpense = expenses.Sum(x => x.Amount);
        return Results.Ok(new { organizationId, asOf = cutoff, openingBalance,
            cumulativeIncome = allIncome, cumulativeExpense = allExpense, cumulativeBalance = openingBalance + allIncome - allExpense,
            monthIncome = monthPayments + monthIncomes, monthExpense = monthExpenses, monthBalance = monthPayments + monthIncomes - monthExpenses,
            pendingExpenses = await db.LodgeTreasuryExpenses.CountAsync(x => x.OrganizationId == organizationId && x.ApprovalStatus == "pending_approval", ct) });
    }

    private static async Task<IResult> GetCashReportAsync(Guid organizationId, DateOnly from, DateOnly to, decimal? observedBalance, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (to < from) return Results.BadRequest(new { message = "El rango de fechas no es válido." });
        var config = await db.LodgeTreasuryConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
        var openingDate = config?.OpeningBalanceDate ?? DateOnly.MinValue;
        var periodFrom = from < openingDate ? openingDate : from;
        var periodOpening = from < openingDate && to >= openingDate && config is not null
            ? config.OpeningBalance
            : await GetBalanceAtStartOfYearAsync(db, organizationId, from, ct);
        var payments = await db.LodgeMemberPayments.AsNoTracking().Include(x => x.Charge).ThenInclude(x => x.Member).ThenInclude(x => x.Person)
            .Where(x => x.Charge.OrganizationId == organizationId && x.PaymentDate >= periodFrom && x.PaymentDate <= to).ToListAsync(ct);
        var incomes = await db.LodgeTreasuryIncomes.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.IncomeDate >= periodFrom && x.IncomeDate <= to).ToListAsync(ct);
        var expenses = await db.LodgeTreasuryExpenses.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.ExpenseDate >= periodFrom && x.ExpenseDate <= to).ToListAsync(ct);
        var movements = payments.Select(x => new { date = x.PaymentDate, type = "ingreso", category = "Cuotas", description = $"Cuota {x.Charge.PeriodMonth:00}/{x.Charge.PeriodYear} · {x.Charge.Member.Person.FirstNames} {x.Charge.Member.Person.LastNames}", amount = x.Amount, status = "registrado", reference = (string?)x.ReceiptNumber, paymentMethod = (string?)x.PaymentMethod })
            .Concat(incomes.Select(x => new { date = x.IncomeDate, type = "ingreso", category = x.Category, description = x.Description, amount = x.Amount, status = "registrado", reference = x.EvidenceReference, paymentMethod = (string?)null }))
            .Concat(expenses.Select(x => new { date = x.ExpenseDate, type = "egreso", category = x.Category, description = x.Description, amount = x.Amount, status = x.ApprovalStatus == "approved" ? "autorizado" : "pendiente de autorización", reference = x.EvidenceReference, paymentMethod = (string?)null }))
            .OrderBy(x => x.date).ToList();
        var received = movements.Where(x => x.type == "ingreso").Sum(x => x.amount);
        var authorized = movements.Where(x => x.type == "egreso" && x.status == "autorizado").Sum(x => x.amount);
        var pending = movements.Where(x => x.type == "egreso" && x.status != "autorizado").Sum(x => x.amount);
        var calculatedBalance = periodOpening + received - authorized;
        var monthlyTotals = movements.GroupBy(x => new { x.date.Year, x.date.Month, x.type, x.category })
            .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month).ThenBy(x => x.Key.type).ThenBy(x => x.Key.category)
            .Select(group =>
            {
                var credit = group.Where(x => x.type == "ingreso").Sum(x => x.amount);
                var debit = group.Where(x => x.type == "egreso" && x.status == "autorizado").Sum(x => x.amount);
                var pendingAmount = group.Where(x => x.type == "egreso" && x.status != "autorizado").Sum(x => x.amount);
                return new { period = $"{group.Key.Year}-{group.Key.Month:00}", type = group.Key.type, category = group.Key.category,
                    debit, credit, net = credit - debit, pendingAmount, count = group.Count() };
            }).ToList();
        return Results.Ok(new { organizationId, from, to, openingBalance = periodOpening, income = received, authorizedExpenses = authorized,
            pendingExpenses = pending, closingBalance = calculatedBalance, observedBalance,
            difference = observedBalance is null ? (decimal?)null : observedBalance.Value - calculatedBalance, monthlyTotals, movements });
    }

    private static async Task<IResult> GetConfigurationAsync(Guid organizationId, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        var config = await db.LodgeTreasuryConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
        return Results.Ok(config is null ? new { organizationId, openingBalance = 0m, openingBalanceDate = DateOnly.FromDateTime(DateTime.UtcNow), incomeCategories = "Otros ingresos", expenseCategories = "Servicios;Materiales;Arriendo;Traslado" } : ToConfiguration(config));
    }

    private static async Task<IResult> SaveConfigurationAsync(Guid organizationId, SaveLodgeTreasuryConfigurationRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (request.OpeningBalance < 0 || string.IsNullOrWhiteSpace(request.IncomeCategories) || string.IsNullOrWhiteSpace(request.ExpenseCategories) ||
            request.IncomeCategories.Length > 2000 || request.ExpenseCategories.Length > 2000)
            return Results.BadRequest(new { message = "El saldo inicial y las listas de categorías deben ser válidos." });
        var config = await db.LodgeTreasuryConfigurations.SingleOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
        if (config is not null && (config.OpeningBalance != request.OpeningBalance || config.OpeningBalanceDate != request.OpeningBalanceDate) &&
            await db.LodgeTreasuryYearClosures.AnyAsync(x => x.OrganizationId == organizationId, ct))
            return Results.Conflict(new { message = "El saldo y fecha de apertura no se pueden cambiar después del primer cierre anual. Registre las correcciones como movimientos auditados en el ejercicio abierto." });
        if (config is null) { config = new LodgeTreasuryConfiguration { OrganizationId = organizationId, OpeningBalance = request.OpeningBalance, OpeningBalanceDate = request.OpeningBalanceDate, IncomeCategories = request.IncomeCategories.Trim(), ExpenseCategories = request.ExpenseCategories.Trim() }; db.LodgeTreasuryConfigurations.Add(config); }
        else { config.OpeningBalance = request.OpeningBalance; config.OpeningBalanceDate = request.OpeningBalanceDate; config.IncomeCategories = request.IncomeCategories.Trim(); config.ExpenseCategories = request.ExpenseCategories.Trim(); config.UpdatedAtUtc = DateTimeOffset.UtcNow; }
        audit.Add(context, "lodge.treasury.configuration.updated", nameof(LodgeTreasuryConfiguration), config.Id.ToString(), organizationId, AuditResults.Success,
            new { config.OpeningBalance, config.OpeningBalanceDate });
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToConfiguration(config));
    }

    private static object ToConfiguration(LodgeTreasuryConfiguration x) => new { organizationId = x.OrganizationId, x.OpeningBalance, x.OpeningBalanceDate, x.IncomeCategories, x.ExpenseCategories };

    private static object ToFeePlan(LodgeFeePlan x, string? territory = null)
    {
        (decimal Amount, string Currency)? official = territory is null
            ? null
            : GrandTreasuryFeeSchedule.Resolve(x.FeeType, territory, x.EffectiveFrom);
        var grandTreasuryAmount = official?.Amount;
        return new { x.Id, x.OrganizationId, x.FeeType, x.MemberAmount,
            grandTreasuryAmount,
            workshopAmount = grandTreasuryAmount is null ? (decimal?)null : x.MemberAmount - grandTreasuryAmount,
            rateAvailable = grandTreasuryAmount is not null,
            x.EffectiveFrom, x.EffectiveUntil, x.IsActive };
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<IResult> CreateExpenseAsync(Guid organizationId, CreateLodgeTreasuryExpenseRequest request, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Description)) return Results.BadRequest(new { message = "Categoría, descripción y monto son obligatorios." });
        if (await IsAccountingYearClosedAsync(db, organizationId, request.ExpenseDate.Year, ct))
            return Results.Conflict(new { message = "La fecha de egreso pertenece a un ejercicio cerrado. Registre el movimiento en un período abierto." });
        var expense = new LodgeTreasuryExpense { OrganizationId = organizationId, Category = request.Category.Trim(), Amount = request.Amount, ExpenseDate = request.ExpenseDate, Description = request.Description.Trim(), EvidenceReference = Normalize(request.EvidenceReference), ApprovalStatus = "pending_approval", RecordedBySubject = context.User.FindFirstValue("sub") ?? "unknown" };
        db.LodgeTreasuryExpenses.Add(expense); audit.Add(context, "lodge.treasury.expense.recorded", nameof(LodgeTreasuryExpense), expense.Id.ToString(), organizationId, AuditResults.Success, new { expense.Category, expense.Amount }); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/gestion-logial/tesoreria/egresos/{expense.Id}", expense);
    }

    private static async Task<IResult> GetExpensesAsync(Guid organizationId, DateOnly? from, DateOnly? to,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId) &&
            !access.CanApproveLodgeExpenses(context.User, organizationId)) return Results.Forbid();
        var start = from ?? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);
        if (end < start) return Results.BadRequest(new { message = "El rango de fechas no es válido." });
        var items = await db.LodgeTreasuryExpenses.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.ExpenseDate >= start && x.ExpenseDate <= end)
            .OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.RecordedAtUtc).ToListAsync(ct);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> ApproveExpenseAsync(Guid expenseId, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var expense = await db.LodgeTreasuryExpenses.SingleOrDefaultAsync(x => x.Id == expenseId, ct); if (expense is null) return Results.NotFound();
        if (!access.CanApproveLodgeExpenses(context.User, expense.OrganizationId)) return Results.Forbid();
        if (expense.ApprovalStatus != "pending_approval") return Results.Conflict(new { message = "El egreso ya fue resuelto." });
        if (await IsAccountingYearClosedAsync(db, expense.OrganizationId, expense.ExpenseDate.Year, ct))
            return Results.Conflict(new { message = "El ejercicio del egreso está cerrado y no admite autorizaciones posteriores." });
        expense.ApprovalStatus = "approved"; expense.ApprovedBySubject = context.User.FindFirstValue("sub") ?? "unknown"; expense.ApprovedAtUtc = DateTimeOffset.UtcNow;
        audit.Add(context, "lodge.treasury.expense.approved", nameof(LodgeTreasuryExpense), expense.Id.ToString(), expense.OrganizationId, AuditResults.Success, new { expense.Category, expense.Amount }); await db.SaveChangesAsync(ct); return Results.Ok(expense);
    }

    private static async Task<IResult> GetAnnualClosuresAsync(Guid organizationId, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        var items = await db.LodgeTreasuryYearClosures.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.AccountingYear).ToListAsync(ct);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> CloseAnnualPeriodAsync(Guid organizationId, int year, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageLodgeTreasury(context.User, organizationId)) return Results.Forbid();
        var chileYear = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/Santiago")).DateTime).Year;
        if (year is < 2000 or > 2200 || year >= chileYear)
            return Results.BadRequest(new { message = "Sólo se puede cerrar un ejercicio anual ya finalizado." });
        if (await db.LodgeTreasuryYearClosures.AnyAsync(x => x.OrganizationId == organizationId && x.AccountingYear == year, ct))
            return Results.Conflict(new { message = "El ejercicio ya está cerrado." });
        var pendingExpenses = await db.LodgeTreasuryExpenses.CountAsync(x => x.OrganizationId == organizationId &&
            x.ExpenseDate.Year == year && x.ApprovalStatus == "pending_approval", ct);
        if (pendingExpenses > 0)
            return Results.Conflict(new { message = $"No se puede cerrar: quedan {pendingExpenses} egresos pendientes de autorización." });

        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);
        var config = await db.LodgeTreasuryConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
        var hasPriorClosure = await db.LodgeTreasuryYearClosures.AnyAsync(x => x.OrganizationId == organizationId && x.AccountingYear < year, ct);
        var opening = config is not null && config.OpeningBalanceDate.Year == year && !hasPriorClosure
            ? config.OpeningBalance
            : await GetBalanceAtStartOfYearAsync(db, organizationId, start, ct);
        var activityStart = config is not null && config.OpeningBalanceDate > start && !hasPriorClosure
            ? config.OpeningBalanceDate : start;
        var payments = await db.LodgeMemberPayments.AsNoTracking().Where(x => x.Charge.OrganizationId == organizationId &&
            x.PaymentDate >= activityStart && x.PaymentDate <= end).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var incomes = await db.LodgeTreasuryIncomes.AsNoTracking().Where(x => x.OrganizationId == organizationId &&
            x.IncomeDate >= activityStart && x.IncomeDate <= end).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var expenses = await db.LodgeTreasuryExpenses.AsNoTracking().Where(x => x.OrganizationId == organizationId &&
            x.ExpenseDate >= activityStart && x.ExpenseDate <= end && x.ApprovalStatus == "approved").SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var movementCount = await db.LodgeMemberPayments.CountAsync(x => x.Charge.OrganizationId == organizationId &&
            x.PaymentDate >= activityStart && x.PaymentDate <= end, ct) +
            await db.LodgeTreasuryIncomes.CountAsync(x => x.OrganizationId == organizationId && x.IncomeDate >= activityStart && x.IncomeDate <= end, ct) +
            await db.LodgeTreasuryExpenses.CountAsync(x => x.OrganizationId == organizationId && x.ExpenseDate >= activityStart &&
                x.ExpenseDate <= end && x.ApprovalStatus == "approved", ct);
        var closure = new LodgeTreasuryYearClosure { OrganizationId = organizationId, AccountingYear = year, OpeningBalance = opening,
            Income = payments + incomes, AuthorizedExpenses = expenses, ClosingBalance = opening + payments + incomes - expenses,
            MovementCount = movementCount, ClosedBySubject = context.User.FindFirstValue("sub") ?? "unknown" };
        db.LodgeTreasuryYearClosures.Add(closure);
        audit.Add(context, "lodge.treasury.year.closed", nameof(LodgeTreasuryYearClosure), closure.Id.ToString(), organizationId,
            AuditResults.Success, new { closure.AccountingYear, closure.OpeningBalance, closure.Income,
                closure.AuthorizedExpenses, closure.ClosingBalance, closure.MovementCount });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/gestion-logial/tesoreria/talleres/{organizationId}/cierres-anuales/{year}", closure);
    }

    private static async Task<bool> IsAccountingYearClosedAsync(PmgmDbContext db, Guid organizationId, int year, CancellationToken ct) =>
        await db.LodgeTreasuryYearClosures.AnyAsync(x => x.OrganizationId == organizationId && x.AccountingYear >= year, ct);

    private static async Task<decimal> GetBalanceAtStartOfYearAsync(PmgmDbContext db, Guid organizationId, DateOnly periodStart, CancellationToken ct)
    {
        var priorClosure = await db.LodgeTreasuryYearClosures.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AccountingYear < periodStart.Year)
            .OrderByDescending(x => x.AccountingYear).FirstOrDefaultAsync(ct);
        var config = await db.LodgeTreasuryConfigurations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId, ct);
        var movementStart = priorClosure is not null
            ? new DateOnly(priorClosure.AccountingYear + 1, 1, 1)
            : config?.OpeningBalanceDate ?? DateOnly.MinValue;
        var balance = priorClosure?.ClosingBalance ??
            (config is not null && config.OpeningBalanceDate <= periodStart ? config.OpeningBalance : 0m);
        if (movementStart >= periodStart) return balance;
        var payments = await db.LodgeMemberPayments.AsNoTracking().Where(x => x.Charge.OrganizationId == organizationId &&
            x.PaymentDate >= movementStart && x.PaymentDate < periodStart).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var incomes = await db.LodgeTreasuryIncomes.AsNoTracking().Where(x => x.OrganizationId == organizationId &&
            x.IncomeDate >= movementStart && x.IncomeDate < periodStart).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var expenses = await db.LodgeTreasuryExpenses.AsNoTracking().Where(x => x.OrganizationId == organizationId &&
            x.ExpenseDate >= movementStart && x.ExpenseDate < periodStart && x.ApprovalStatus == "approved")
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        return balance + payments + incomes - expenses;
    }
}

public sealed record CreateLodgeFeePlanRequest(string FeeType, decimal MemberAmount, decimal GrandTreasuryAmount,
    DateOnly EffectiveFrom, DateOnly? EffectiveUntil);
public sealed record LodgeFeeAssignmentRequest(Guid MemberId, string FeeType);
public sealed record GenerateLodgeChargesRequest(int PeriodYear, int PeriodMonth, IReadOnlyList<LodgeFeeAssignmentRequest>? Assignments);
public sealed record AddLodgeMemberPaymentRequest(decimal Amount, string PaymentMethod, DateOnly PaymentDate, string? Reference, string? IdempotencyKey);
public sealed record CreateLodgeTreasuryExpenseRequest(string Category, decimal Amount, DateOnly ExpenseDate, string Description, string? EvidenceReference);
public sealed record CreateLodgeTreasuryIncomeRequest(string Category, decimal Amount, DateOnly IncomeDate, string Description, string? EvidenceReference);
public sealed record SaveLodgeTreasuryConfigurationRequest(decimal OpeningBalance, DateOnly OpeningBalanceDate, string IncomeCategories, string ExpenseCategories);
