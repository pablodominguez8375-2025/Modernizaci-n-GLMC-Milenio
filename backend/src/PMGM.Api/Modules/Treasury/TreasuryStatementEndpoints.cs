using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static class TreasuryStatementEndpoints
{
    public static RouteGroupBuilder MapTreasuryStatementEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/talleres/{organizationId:guid}/cuadros", CreateAsync);
        group.MapPost("/cuadros/{statementId:guid}/lineas", AddLineAsync);
        group.MapPost("/cuadros/{statementId:guid}/generar-lineas", GenerateLinesAsync);
        group.MapPost("/cuadros/{statementId:guid}/pagos", AddPaymentAsync);
        group.MapPost("/cuadros/{statementId:guid}/enviar", SubmitAsync);
        group.MapPost("/cuadros/{statementId:guid}/conciliar", ReconcileAsync);
        group.MapGet("/cuadros/{statementId:guid}", GetAsync);
        return group;
    }

    private static async Task<IResult> GenerateLinesAsync(Guid statementId, GenerateTreasuryStatementLinesRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        if (request.ApprenticeAmount < 0 || request.FellowcraftAmount < 0 || request.MasterAmount < 0)
            return Results.BadRequest(new { message = "Las cuotas base no pueden ser negativas." });

        var statement = await db.TreasuryMonthlyStatements.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == statementId, cancellationToken);
        if (statement is null) return Results.NotFound();
        if (statement.Status != TreasuryCodes.StatementStatus.Draft || statement.Lines.Count != 0)
            return Results.Conflict(new { message = "La generación automática requiere un cuadro vacío en borrador." });

        var cutoff = statement.CutoffDate;
        var memberships = await db.Memberships.AsNoTracking()
            .Where(x => x.OrganizationId == statement.OrganizationId && x.StartDate <= cutoff &&
                        (x.EndDate == null || x.EndDate >= cutoff))
            .OrderBy(x => x.StartDate)
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0)
            return Results.Conflict(new { message = "El Taller no tiene miembros vigentes a la fecha de corte." });

        var memberIds = memberships.Select(x => x.MemberId).Distinct().ToArray();
        var degreeEvents = await db.DegreeEvents.AsNoTracking()
            .Where(x => x.OrganizationId == statement.OrganizationId && memberIds.Contains(x.MemberId) && x.EffectiveDate <= cutoff)
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.RecordedAtUtc)
            .ToListAsync(cancellationToken);
        var offices = await db.OfficeAssignments.AsNoTracking()
            .Where(x => x.OrganizationId == statement.OrganizationId && memberIds.Contains(x.MemberId) &&
                        x.StartDate <= cutoff && (x.EndDate == null || x.EndDate >= cutoff))
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var adjustments = await db.TreasuryAdjustments.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.Status == TreasuryCodes.AdjustmentStatus.Active &&
                        (x.OrganizationId == null || x.OrganizationId == statement.OrganizationId) &&
                        x.EffectiveFrom <= cutoff && (x.EffectiveUntil == null || x.EffectiveUntil >= cutoff))
            .OrderByDescending(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var missingDegreeMembers = memberships
            .Where(x => degreeEvents.All(d => d.MemberId != x.MemberId))
            .Select(x => x.MemberId).Distinct().ToArray();
        var multipleAdjustmentMembers = adjustments.GroupBy(x => x.MemberId)
            .Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (missingDegreeMembers.Length != 0 || multipleAdjustmentMembers.Length != 0)
            return Results.Conflict(new
            {
                message = "No fue posible generar líneas por antecedentes incompletos o incompatibles.",
                missingDegreeMembers,
                multipleAdjustmentMembers
            });

        foreach (var membership in memberships)
        {
            var degree = degreeEvents.First(x => x.MemberId == membership.MemberId).Degree;
            var baseAmount = request.AmountFor(degree);
            if (baseAmount is null)
                return Results.BadRequest(new { message = $"El grado '{degree}' no tiene una cuota base configurada." });
            var office = offices.FirstOrDefault(x => x.MemberId == membership.MemberId);
            var adjustment = adjustments.FirstOrDefault(x => x.MemberId == membership.MemberId);
            var adjustmentAmount = adjustment?.Amount ?? 0m;
            if (baseAmount.Value + adjustmentAmount < 0)
                return Results.Conflict(new { message = "Un ajuste deja una cuota individual negativa.", memberId = membership.MemberId });

            var line = new TreasuryMonthlyStatementLine
            {
                StatementId = statement.Id,
                MemberId = membership.MemberId,
                MembershipId = membership.Id,
                DegreeCodeAtCutoff = degree,
                OfficeCodeAtCutoff = office?.OfficeType,
                BaseAmount = baseAmount.Value,
                AdjustmentAmount = adjustmentAmount,
                AdjustmentType = adjustment?.AdjustmentType,
                AuthorizationReference = adjustment?.AuthorizationReference,
                IdentityMatchStatus = TreasuryCodes.IdentityMatchStatus.Matched
            };
            db.TreasuryMonthlyStatementLines.Add(line);
        }

        audit.Add(context, "treasury.statement.lines_generated", nameof(TreasuryMonthlyStatement), statement.Id.ToString(),
            statement.OrganizationId, AuditResults.Success, new { cutoff, lines = statement.Lines.Count });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(statement));
    }

    private static async Task<IResult> CreateAsync(Guid organizationId, CreateTreasuryStatementRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        if (request.PeriodYear is < 2000 or > 2200 || request.PeriodMonth is < 1 or > 12)
            return Results.BadRequest(new { message = "El período indicado no es válido." });
        if (!await db.Organizations.AnyAsync(x => x.Id == organizationId, cancellationToken))
            return Results.NotFound(new { message = "El Taller no existe." });
        if (await db.TreasuryMonthlyStatements.AnyAsync(x => x.OrganizationId == organizationId &&
            x.PeriodYear == request.PeriodYear && x.PeriodMonth == request.PeriodMonth && x.RectifiesStatementId == null,
            cancellationToken))
            return Results.Conflict(new { message = "Ya existe un cuadro activo para el Taller y período." });

        var statement = new TreasuryMonthlyStatement
        {
            OrganizationId = organizationId,
            PeriodYear = request.PeriodYear,
            PeriodMonth = request.PeriodMonth,
            CutoffDate = request.CutoffDate,
            Status = TreasuryCodes.StatementStatus.Draft,
            SourceReference = request.SourceReference
        };
        db.TreasuryMonthlyStatements.Add(statement);
        audit.Add(context, "treasury.statement.created", nameof(TreasuryMonthlyStatement), statement.Id.ToString(),
            organizationId, AuditResults.Success, new { statement.PeriodYear, statement.PeriodMonth });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/tesoreria/cuadros/{statement.Id}", ToResponse(statement));
    }

    private static async Task<IResult> AddLineAsync(Guid statementId, AddTreasuryStatementLineRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        var statement = await db.TreasuryMonthlyStatements.FindAsync([statementId], cancellationToken);
        if (statement is null) return Results.NotFound();
        if (statement.Status != TreasuryCodes.StatementStatus.Draft)
            return Results.Conflict(new { message = "Sólo se pueden modificar cuadros en borrador." });
        if (!TreasuryCodes.IdentityMatchStatus.IsValid(request.IdentityMatchStatus))
            return Results.BadRequest(new { message = "El estado de identidad no es válido." });
        if (request.IdentityMatchStatus == TreasuryCodes.IdentityMatchStatus.Matched && request.MemberId is null)
            return Results.BadRequest(new { message = "Una identidad conciliada debe estar vinculada a un hermano." });
        if (request.BaseAmount < 0 || request.BaseAmount + request.AdjustmentAmount < 0)
            return Results.BadRequest(new { message = "La cuota resultante no puede ser negativa." });
        if (request.AdjustmentAmount != 0 && string.IsNullOrWhiteSpace(request.AuthorizationReference))
            return Results.BadRequest(new { message = "Toda rebaja o ajuste requiere una referencia de autorización." });
        if (request.MemberId is not null && !await db.Members.AnyAsync(x => x.Id == request.MemberId, cancellationToken))
            return Results.NotFound(new { message = "El hermano no existe." });
        if (request.MembershipId is not null && !await db.Memberships.AnyAsync(x => x.Id == request.MembershipId &&
            x.OrganizationId == statement.OrganizationId && x.MemberId == request.MemberId, cancellationToken))
            return Results.BadRequest(new { message = "La pertenencia no corresponde al hermano y Taller del cuadro." });

        var line = new TreasuryMonthlyStatementLine
        {
            StatementId = statementId,
            MemberId = request.MemberId,
            MembershipId = request.MembershipId,
            DegreeCodeAtCutoff = request.DegreeCodeAtCutoff,
            OfficeCodeAtCutoff = request.OfficeCodeAtCutoff,
            BaseAmount = request.BaseAmount,
            AdjustmentAmount = request.AdjustmentAmount,
            AdjustmentType = request.AdjustmentType,
            AuthorizationReference = request.AuthorizationReference,
            Observation = request.Observation,
            IdentityMatchStatus = request.IdentityMatchStatus
        };
        db.TreasuryMonthlyStatementLines.Add(line);
        audit.Add(context, "treasury.statement.line_added", nameof(TreasuryMonthlyStatementLine), line.Id.ToString(),
            statement.OrganizationId, AuditResults.Success, new { statementId, line.DegreeCodeAtCutoff, line.BaseAmount, line.AdjustmentAmount });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/tesoreria/cuadros/{statementId}", new { line.Id, line.PayableAmount });
    }

    private static async Task<IResult> AddPaymentAsync(Guid statementId, AddTreasuryPaymentRequest request,
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        var statement = await db.TreasuryMonthlyStatements.FindAsync([statementId], cancellationToken);
        if (statement is null) return Results.NotFound();
        if (statement.Status is TreasuryCodes.StatementStatus.Reconciled or TreasuryCodes.StatementStatus.Closed or TreasuryCodes.StatementStatus.Rectified)
            return Results.Conflict(new { message = "El cuadro ya no admite pagos." });
        if (!TreasuryCodes.PaymentMethod.IsValid(request.PaymentMethod) || request.Amount <= 0)
            return Results.BadRequest(new { message = "El medio de pago o el monto no es válido." });

        var payment = new TreasuryPayment
        {
            StatementId = statementId,
            PaymentMethod = request.PaymentMethod,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            PayerDisplayName = request.PayerDisplayName,
            PayerRut = request.PayerRut,
            Reference = request.Reference
        };
        db.TreasuryPayments.Add(payment);
        audit.Add(context, "treasury.statement.payment_added", nameof(TreasuryPayment), payment.Id.ToString(),
            statement.OrganizationId, AuditResults.Success, new { statementId, payment.PaymentMethod, payment.Amount });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/tesoreria/cuadros/{statementId}", new { payment.Id });
    }

    private static async Task<IResult> SubmitAsync(Guid statementId, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        var statement = await db.TreasuryMonthlyStatements.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == statementId, cancellationToken);
        if (statement is null) return Results.NotFound();
        if (statement.Status != TreasuryCodes.StatementStatus.Draft || statement.Lines.Count == 0)
            return Results.Conflict(new { message = "El cuadro debe estar en borrador y contener líneas antes de enviarse." });
        statement.Status = TreasuryCodes.StatementStatus.Submitted;
        statement.SubmittedAtUtc = DateTimeOffset.UtcNow;
        audit.Add(context, "treasury.statement.submitted", nameof(TreasuryMonthlyStatement), statement.Id.ToString(),
            statement.OrganizationId, AuditResults.Success, new { lines = statement.Lines.Count });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(statement));
    }

    private static async Task<IResult> ReconcileAsync(Guid statementId, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken cancellationToken)
    {
        if (!access.CanManageTreasuryRegularity(context.User)) return Results.Forbid();
        var statement = await db.TreasuryMonthlyStatements.Include(x => x.Lines).Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == statementId, cancellationToken);
        if (statement is null) return Results.NotFound();
        if (statement.Status is not (TreasuryCodes.StatementStatus.Submitted or TreasuryCodes.StatementStatus.Observed))
            return Results.Conflict(new { message = "El cuadro debe estar enviado u observado para conciliarse." });
        var totals = TreasuryStatementTotals.Calculate(statement.Lines, statement.Payments);
        var unresolved = statement.Lines.Count(x => x.IdentityMatchStatus != TreasuryCodes.IdentityMatchStatus.Matched);
        if (totals.DifferenceAmount != 0 || unresolved != 0)
            return Results.Conflict(new { message = "El cuadro mantiene diferencias u observaciones de identidad.", totals, unresolvedIdentities = unresolved });

        statement.Status = TreasuryCodes.StatementStatus.Reconciled;
        statement.ReconciledAtUtc = DateTimeOffset.UtcNow;
        var snapshot = new FinancialRegularitySnapshot
        {
            OrganizationId = statement.OrganizationId,
            Scope = TreasuryCodes.RegularityScope.Organization,
            Status = TreasuryCodes.RegularityStatus.UpToDate,
            AsOfDate = statement.CutoffDate,
            SourceReference = $"TREASURY-STATEMENT:{statement.Id}",
            Notes = $"Cuadro {statement.PeriodYear:D4}-{statement.PeriodMonth:D2} conciliado."
        };
        db.FinancialRegularitySnapshots.Add(snapshot);
        audit.Add(context, "treasury.statement.reconciled", nameof(TreasuryMonthlyStatement), statement.Id.ToString(),
            statement.OrganizationId, AuditResults.Success, new { totals.ExpectedAmount, totals.PaidAmount, snapshotId = snapshot.Id });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(statement));
    }

    private static async Task<IResult> GetAsync(Guid statementId, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken cancellationToken)
    {
        var statement = await db.TreasuryMonthlyStatements.AsNoTracking().Include(x => x.Lines).Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == statementId, cancellationToken);
        if (statement is null) return Results.NotFound();
        if (!access.CanManageTreasuryRegularity(context.User) && !access.CanReadOrganization(context.User, statement.OrganizationId))
            return Results.Forbid();
        return Results.Ok(ToResponse(statement));
    }

    private static object ToResponse(TreasuryMonthlyStatement statement)
    {
        var totals = TreasuryStatementTotals.Calculate(statement.Lines, statement.Payments);
        return new
        {
            statement.Id, statement.OrganizationId, statement.PeriodYear, statement.PeriodMonth, statement.CutoffDate,
            statement.Status, statement.SourceReference, totals.ExpectedAmount, totals.TransferAmount,
            totals.DepositAmount, totals.PaidAmount, totals.DifferenceAmount,
            unresolvedIdentities = statement.Lines.Count(x => x.IdentityMatchStatus != TreasuryCodes.IdentityMatchStatus.Matched),
            lines = statement.Lines.Select(x => new { x.Id, x.MemberId, x.MembershipId, x.DegreeCodeAtCutoff,
                x.OfficeCodeAtCutoff, x.BaseAmount, x.AdjustmentAmount, x.PayableAmount, x.AdjustmentType,
                x.AuthorizationReference, x.Observation, x.IdentityMatchStatus }),
            payments = statement.Payments.Select(x => new { x.Id, x.PaymentMethod, x.PaymentDate, x.Amount,
                x.PayerDisplayName, x.Reference, x.RecordedAtUtc }),
            statement.SubmittedAtUtc, statement.ReconciledAtUtc, statement.ClosedAtUtc
        };
    }
}

public static class TreasuryStatementTotals
{
    public static TreasuryStatementTotalsDto Calculate(IEnumerable<TreasuryMonthlyStatementLine> lines,
        IEnumerable<TreasuryPayment> payments)
    {
        var lineRows = lines.ToList();
        var paymentRows = payments.ToList();
        var expected = lineRows.Sum(x => x.PayableAmount);
        var transfers = paymentRows.Where(x => x.PaymentMethod == TreasuryCodes.PaymentMethod.Transfer).Sum(x => x.Amount);
        var deposits = paymentRows.Where(x => x.PaymentMethod == TreasuryCodes.PaymentMethod.Deposit).Sum(x => x.Amount);
        return new(expected, transfers, deposits, transfers + deposits, expected - transfers - deposits);
    }
}

public sealed record TreasuryStatementTotalsDto(decimal ExpectedAmount, decimal TransferAmount,
    decimal DepositAmount, decimal PaidAmount, decimal DifferenceAmount);
public sealed record CreateTreasuryStatementRequest(int PeriodYear, int PeriodMonth, DateOnly CutoffDate, string? SourceReference);
public sealed record AddTreasuryStatementLineRequest(Guid? MemberId, Guid? MembershipId, string DegreeCodeAtCutoff,
    string? OfficeCodeAtCutoff, decimal BaseAmount, decimal AdjustmentAmount, string? AdjustmentType,
    string? AuthorizationReference, string? Observation, string IdentityMatchStatus);
public sealed record AddTreasuryPaymentRequest(string PaymentMethod, DateOnly PaymentDate, decimal Amount,
    string PayerDisplayName, string? PayerRut, string? Reference);
public sealed record GenerateTreasuryStatementLinesRequest(decimal ApprenticeAmount, decimal FellowcraftAmount, decimal MasterAmount)
{
    public decimal? AmountFor(string degree) => degree switch
    {
        TreasuryCodes.Degree.Apprentice => ApprenticeAmount,
        TreasuryCodes.Degree.Fellowcraft => FellowcraftAmount,
        TreasuryCodes.Degree.Master => MasterAmount,
        _ => null
    };
}
