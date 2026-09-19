using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static class LodgeHospitalariaEndpoints
{
    public static IEndpointRouteBuilder MapLodgeHospitalariaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial/hospitalaria")
            .WithTags("Hospitalaria del Taller")
            .RequireAuthorization();

        group.MapPost("/talleres/{organizationId:guid}/movimientos", CreateMovementAsync);
        group.MapPost("/movimientos/{movementId:guid}/aprobar", ApproveMovementAsync);
        group.MapPost("/movimientos/{movementId:guid}/aprobar-consejo", ApproveMovementByCouncilAsync);
        group.MapGet("/talleres/{organizationId:guid}/resumen", GetSummaryAsync);
        group.MapGet("/talleres/{organizationId:guid}/acuerdos-socorro", GetCouncilAidDecisionsAsync);
        group.MapGet("/talleres/{organizationId:guid}/revisiones-consejo", GetCouncilFinancialReviewsAsync);
        group.MapPut("/talleres/{organizationId:guid}/rendiciones/{year:int}/{month:int}", UpsertMonthlySubmissionAsync);
        group.MapGet("/talleres/{organizationId:guid}/rendiciones", GetMonthlySubmissionsAsync);
        group.MapPost("/rendiciones/{submissionId:guid}/enviar", SubmitMonthlySubmissionAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateMovementAsync(
        Guid organizationId,
        CreateLodgeHospitalariaMovementRequest request,
        HttpContext context,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        if (!access.CanManageLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();
        if (request.Amount <= 0 ||
            !HospitalariaMovementCodes.IsValidType(request.MovementType) ||
            !HospitalariaMovementCodes.IsValidCategory(request.Category))
            return Results.BadRequest(new { message = "Tipo, categoría y monto del movimiento deben ser válidos." });

        if (request.MovementType == HospitalariaMovementCodes.Expense &&
            string.IsNullOrWhiteSpace(request.EvidenceReference))
            return Results.BadRequest(new { message = "Todo egreso de Hospitalaria debe conservar una referencia de respaldo." });

        var movement = new LodgeHospitalariaMovement
        {
            OrganizationId = organizationId,
            MovementType = request.MovementType,
            Category = request.Category,
            Amount = request.Amount,
            MovementDate = request.MovementDate,
            MemberReference = Normalize(request.MemberReference),
            Destination = Normalize(request.Destination),
            EvidenceReference = Normalize(request.EvidenceReference),
            Observation = Normalize(request.Observation),
            ApprovalStatus = request.MovementType == HospitalariaMovementCodes.Expense
                ? HospitalariaMovementCodes.PendingApproval
                : HospitalariaMovementCodes.NotRequired,
            ApprovalSource = null,
            CouncilDecisionId = null,
            RecordedBySubject = GetSubject(context.User)
        };

        db.LodgeHospitalariaMovements.Add(movement);
        audit.Add(
            context,
            "lodge.hospitalaria.movement.recorded",
            nameof(LodgeHospitalariaMovement),
            movement.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { movement.MovementType, movement.Category, movement.Amount, movement.MovementDate });
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/gestion-logial/hospitalaria/movimientos/{movement.Id}", ToMovementDto(movement));
    }

    private static async Task<IResult> ApproveMovementAsync(
        Guid movementId,
        HttpContext context,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        var movement = await db.LodgeHospitalariaMovements.SingleOrDefaultAsync(x => x.Id == movementId, ct);
        if (movement is null) return Results.NotFound();
        if (movement.MovementType != HospitalariaMovementCodes.Expense)
            return Results.BadRequest(new { message = "Solo los egresos requieren aprobación." });
        if (!access.CanApproveLodgeExpenses(context.User, movement.OrganizationId)) return Results.Forbid();
        if (movement.ApprovalStatus != HospitalariaMovementCodes.PendingApproval)
            return Results.Conflict(new { message = "El egreso ya fue aprobado o no está pendiente." });

        movement.ApprovalStatus = HospitalariaMovementCodes.Approved;
        movement.ApprovalSource = HospitalariaCodes.ApprovalSource.VenerableMaster;
        movement.CouncilDecisionId = null;
        movement.ApprovedBySubject = GetSubject(context.User);
        movement.ApprovedAtUtc = DateTimeOffset.UtcNow;

        audit.Add(
            context,
            "lodge.hospitalaria.expense.approved_by_venerable",
            nameof(LodgeHospitalariaMovement),
            movement.Id.ToString(),
            movement.OrganizationId,
            AuditResults.Success,
            new { movement.Amount, movement.Category });
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToMovementDto(movement));
    }

    private static async Task<IResult> ApproveMovementByCouncilAsync(
        Guid movementId,
        CouncilHospitalariaApprovalRequest request,
        HttpContext context,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        var movement = await db.LodgeHospitalariaMovements.SingleOrDefaultAsync(x => x.Id == movementId, ct);
        if (movement is null) return Results.NotFound();
        if (!access.CanManageLodgeHospitalaria(context.User, movement.OrganizationId)) return Results.Forbid();
        if (movement.MovementType != HospitalariaMovementCodes.Expense ||
            movement.Category != HospitalariaMovementCodes.CharityAid)
            return Results.BadRequest(new { message = "La vía de Consejo sólo aplica a socorros/ayudas de beneficencia." });
        if (movement.ApprovalStatus != HospitalariaMovementCodes.PendingApproval)
            return Results.Conflict(new { message = "El egreso ya fue aprobado o no está pendiente." });

        var decision = await lodgeDb.LodgeCouncilDecisions
            .AsNoTracking()
            .Include(x => x.Session)
            .SingleOrDefaultAsync(x => x.Id == request.CouncilDecisionId, ct);

        var valid = decision is not null &&
                    decision.Session.OrganizationId == movement.OrganizationId &&
                    decision.Category == LodgeCouncilCodes.DecisionCategory.BenevolenceAidProposal &&
                    decision.Outcome == LodgeCouncilCodes.DecisionOutcome.Approved &&
                    decision.Amount == movement.Amount;

        if (!valid)
            return Results.BadRequest(new
            {
                message = "El acuerdo del Consejo debe pertenecer al mismo Taller, aprobar un socorro y coincidir exactamente con el monto del egreso."
            });

        movement.ApprovalStatus = HospitalariaMovementCodes.Approved;
        movement.ApprovalSource = HospitalariaCodes.ApprovalSource.LodgeCouncil;
        movement.CouncilDecisionId = decision!.Id;
        movement.ApprovedBySubject = $"council:{decision.Id}";
        movement.ApprovedAtUtc = DateTimeOffset.UtcNow;

        audit.Add(
            context,
            "lodge.hospitalaria.expense.approved_by_council",
            nameof(LodgeHospitalariaMovement),
            movement.Id.ToString(),
            movement.OrganizationId,
            AuditResults.Success,
            new { movement.Amount, movement.Category, councilDecisionId = decision.Id, councilSessionId = decision.SessionId });
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToMovementDto(movement));
    }

    private static async Task<IResult> GetSummaryAsync(
        Guid organizationId,
        DateOnly? from,
        DateOnly? to,
        HttpContext context,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken ct)
    {
        if (!access.CanReadLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();

        var start = from ?? FirstDayOfCurrentMonthInChile();
        var end = to ?? start.AddMonths(1).AddDays(-1);
        if (end < start) return Results.BadRequest(new { message = "La fecha final no puede ser anterior a la inicial." });

        var rows = await db.LodgeHospitalariaMovements
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.MovementDate >= start && x.MovementDate <= end)
            .OrderByDescending(x => x.MovementDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .ToListAsync(ct);

        var income = rows.Where(x => x.MovementType == HospitalariaMovementCodes.Income).Sum(x => x.Amount);
        var approvedExpenses = rows
            .Where(x => x.MovementType == HospitalariaMovementCodes.Expense &&
                        x.ApprovalStatus == HospitalariaMovementCodes.Approved)
            .Sum(x => x.Amount);
        var pendingExpenses = rows.Count(x =>
            x.MovementType == HospitalariaMovementCodes.Expense &&
            x.ApprovalStatus == HospitalariaMovementCodes.PendingApproval);

        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            organizationId,
            from = start,
            to = end,
            income,
            approvedExpenses,
            periodNet = income - approvedExpenses,
            pendingExpenses,
            movements = rows.Count,
            categories = rows
                .GroupBy(x => x.Category)
                .Select(g => new { category = g.Key, total = g.Sum(x => x.Amount), count = g.Count() }),
            items = rows.Select(ToMovementDto)
        });
    }

    private static async Task<IResult> GetCouncilAidDecisionsAsync(
        Guid organizationId,
        DateOnly? from,
        DateOnly? to,
        HttpContext context,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        CancellationToken ct)
    {
        if (!access.CanManageLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();

        var query = lodgeDb.LodgeCouncilDecisions
            .AsNoTracking()
            .Include(x => x.Session)
            .Where(x =>
                x.Session.OrganizationId == organizationId &&
                x.Category == LodgeCouncilCodes.DecisionCategory.BenevolenceAidProposal &&
                x.Outcome == LodgeCouncilCodes.DecisionOutcome.Approved);

        if (from is not null) query = query.Where(x => x.Session.SessionDate >= from.Value);
        if (to is not null) query = query.Where(x => x.Session.SessionDate <= to.Value);

        var items = await query
            .OrderByDescending(x => x.Session.SessionDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Take(100)
            .Select(x => new
            {
                id = x.Id,
                sessionId = x.SessionId,
                sessionDate = x.Session.SessionDate,
                x.Subject,
                x.Amount
            })
            .ToListAsync(ct);

        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> GetCouncilFinancialReviewsAsync(
        Guid organizationId,
        HttpContext context,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        CancellationToken ct)
    {
        if (!access.CanManageLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();

        var items = await lodgeDb.LodgeCouncilFinancialReviews
            .AsNoTracking()
            .Include(x => x.Session)
            .Where(x =>
                x.Session.OrganizationId == organizationId &&
                x.ControlArea == LodgeCouncilCodes.ControlArea.Hospitalaria)
            .OrderByDescending(x => x.Session.SessionDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Take(120)
            .Select(x => new
            {
                id = x.Id,
                sessionId = x.SessionId,
                sessionDate = x.Session.SessionDate,
                x.PeriodLabel,
                x.Conclusion
            })
            .ToListAsync(ct);

        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> UpsertMonthlySubmissionAsync(
        Guid organizationId,
        int year,
        int month,
        UpsertHospitalariaMonthlySubmissionRequest request,
        HttpContext context,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        if (!access.CanManageLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
            return Results.BadRequest(new { message = "El período indicado no es válido." });
        if (request.ReplenishmentDueAmount < 0 || request.ReplenishmentPaidAmount < 0)
            return Results.BadRequest(new { message = "Los montos de reposición no pueden ser negativos." });
        if (request.ReplenishmentPaidAmount > 0 && string.IsNullOrWhiteSpace(request.PaymentReference))
            return Results.BadRequest(new { message = "Debe registrar la referencia/comprobante de la reposición pagada." });

        var existing = await db.HospitalariaMonthlySubmissions
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.PeriodYear == year && x.PeriodMonth == month, ct);

        if (existing is not null &&
            existing.Status is HospitalariaCodes.SubmissionStatus.Submitted or HospitalariaCodes.SubmissionStatus.Reconciled)
            return Results.Conflict(new { message = "La rendición enviada o conciliada no puede modificarse." });

        var totals = await CalculatePeriodTotalsAsync(organizationId, year, month, db, ct);

        var submission = existing ?? new HospitalariaMonthlySubmission
        {
            OrganizationId = organizationId,
            PeriodYear = year,
            PeriodMonth = month,
            CutoffDate = new DateOnly(year, month, DateTime.DaysInMonth(year, month)),
            Status = HospitalariaCodes.SubmissionStatus.Draft,
            CreatedBySubject = GetSubject(context.User)
        };

        submission.IncomeAmount = totals.IncomeAmount;
        submission.ApprovedExpenseAmount = totals.ApprovedExpenseAmount;
        submission.PeriodNetAmount = totals.IncomeAmount - totals.ApprovedExpenseAmount;
        submission.MovementCount = totals.MovementCount;
        submission.PendingExpenseCount = totals.PendingExpenseCount;
        submission.ReplenishmentDueAmount = request.ReplenishmentDueAmount;
        submission.ReplenishmentPaidAmount = request.ReplenishmentPaidAmount;
        submission.DifferenceAmount = request.ReplenishmentDueAmount - request.ReplenishmentPaidAmount;
        submission.PaymentReference = Normalize(request.PaymentReference);
        submission.CouncilFinancialReviewId = request.CouncilFinancialReviewId;
        submission.SourceReference = Normalize(request.SourceReference);
        submission.Status = HospitalariaCodes.SubmissionStatus.Draft;
        submission.ReviewNotes = null;
        submission.ReviewedAtUtc = null;
        submission.ReviewedBySubject = null;

        if (existing is null) db.HospitalariaMonthlySubmissions.Add(submission);

        audit.Add(
            context,
            "lodge.hospitalaria.monthly_submission.saved",
            nameof(HospitalariaMonthlySubmission),
            submission.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new
            {
                year,
                month,
                submission.IncomeAmount,
                submission.ApprovedExpenseAmount,
                submission.PendingExpenseCount,
                submission.ReplenishmentDueAmount,
                submission.ReplenishmentPaidAmount,
                submission.DifferenceAmount,
                submission.CouncilFinancialReviewId
            });

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToSubmissionDto(submission));
    }

    private static async Task<IResult> GetMonthlySubmissionsAsync(
        Guid organizationId,
        int? year,
        int? month,
        HttpContext context,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken ct)
    {
        if (!access.CanReadLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();

        var query = db.HospitalariaMonthlySubmissions
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId);
        if (year is not null) query = query.Where(x => x.PeriodYear == year.Value);
        if (month is not null) query = query.Where(x => x.PeriodMonth == month.Value);

        var items = await query
            .OrderByDescending(x => x.PeriodYear)
            .ThenByDescending(x => x.PeriodMonth)
            .Take(36)
            .ToListAsync(ct);

        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items = items.Select(ToSubmissionDto) });
    }

    private static async Task<IResult> SubmitMonthlySubmissionAsync(
        Guid submissionId,
        HttpContext context,
        PmgmDbContext db,
        LodgeManagementDbContext lodgeDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken ct)
    {
        var submission = await db.HospitalariaMonthlySubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, ct);
        if (submission is null) return Results.NotFound();
        if (!access.CanManageLodgeHospitalaria(context.User, submission.OrganizationId)) return Results.Forbid();
        if (submission.Status != HospitalariaCodes.SubmissionStatus.Draft)
            return Results.Conflict(new { message = "Sólo una rendición en borrador puede enviarse." });

        var totals = await CalculatePeriodTotalsAsync(
            submission.OrganizationId,
            submission.PeriodYear,
            submission.PeriodMonth,
            db,
            ct);

        submission.IncomeAmount = totals.IncomeAmount;
        submission.ApprovedExpenseAmount = totals.ApprovedExpenseAmount;
        submission.PeriodNetAmount = totals.IncomeAmount - totals.ApprovedExpenseAmount;
        submission.MovementCount = totals.MovementCount;
        submission.PendingExpenseCount = totals.PendingExpenseCount;

        if (submission.PendingExpenseCount != 0)
            return Results.Conflict(new { message = "No puede enviarse la rendición mientras existan egresos pendientes de autorización." });
        if (submission.CouncilFinancialReviewId is null)
            return Results.Conflict(new { message = "Debe vincular la revisión mensual de Hospitalaria del Consejo de Administración." });
        if (submission.ReplenishmentPaidAmount > 0 && string.IsNullOrWhiteSpace(submission.PaymentReference))
            return Results.Conflict(new { message = "Debe registrar la referencia/comprobante de la reposición pagada." });

        var councilReview = await lodgeDb.LodgeCouncilFinancialReviews
            .AsNoTracking()
            .Include(x => x.Session)
            .SingleOrDefaultAsync(x => x.Id == submission.CouncilFinancialReviewId.Value, ct);

        if (councilReview is null ||
            councilReview.Session.OrganizationId != submission.OrganizationId ||
            councilReview.ControlArea != LodgeCouncilCodes.ControlArea.Hospitalaria)
            return Results.BadRequest(new { message = "La revisión del Consejo no corresponde a Hospitalaria de este Taller." });

        submission.Status = HospitalariaCodes.SubmissionStatus.Submitted;
        submission.SubmittedAtUtc = DateTimeOffset.UtcNow;

        audit.Add(
            context,
            "lodge.hospitalaria.monthly_submission.submitted",
            nameof(HospitalariaMonthlySubmission),
            submission.Id.ToString(),
            submission.OrganizationId,
            AuditResults.Success,
            new
            {
                submission.PeriodYear,
                submission.PeriodMonth,
                submission.IncomeAmount,
                submission.ApprovedExpenseAmount,
                submission.PeriodNetAmount,
                submission.ReplenishmentDueAmount,
                submission.ReplenishmentPaidAmount,
                submission.DifferenceAmount,
                submission.CouncilFinancialReviewId
            });

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToSubmissionDto(submission));
    }

    private static async Task<HospitalariaPeriodTotals> CalculatePeriodTotalsAsync(
        Guid organizationId,
        int year,
        int month,
        PmgmDbContext db,
        CancellationToken ct)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var rows = await db.LodgeHospitalariaMovements
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.MovementDate >= start && x.MovementDate <= end)
            .ToListAsync(ct);

        return new HospitalariaPeriodTotals(
            rows.Where(x => x.MovementType == HospitalariaMovementCodes.Income).Sum(x => x.Amount),
            rows.Where(x => x.MovementType == HospitalariaMovementCodes.Expense &&
                            x.ApprovalStatus == HospitalariaMovementCodes.Approved).Sum(x => x.Amount),
            rows.Count,
            rows.Count(x => x.MovementType == HospitalariaMovementCodes.Expense &&
                            x.ApprovalStatus == HospitalariaMovementCodes.PendingApproval));
    }

    private static object ToMovementDto(LodgeHospitalariaMovement movement) => new
    {
        movement.Id,
        movement.OrganizationId,
        movement.MovementType,
        movement.Category,
        movement.Amount,
        movement.MovementDate,
        movement.MemberReference,
        movement.Destination,
        movement.EvidenceReference,
        movement.Observation,
        movement.ApprovalStatus,
        movement.ApprovalSource,
        movement.CouncilDecisionId,
        movement.ApprovedBySubject,
        movement.ApprovedAtUtc,
        movement.RecordedAtUtc
    };

    private static object ToSubmissionDto(HospitalariaMonthlySubmission submission) => new
    {
        submission.Id,
        submission.OrganizationId,
        submission.PeriodYear,
        submission.PeriodMonth,
        submission.CutoffDate,
        submission.IncomeAmount,
        submission.ApprovedExpenseAmount,
        submission.PeriodNetAmount,
        submission.MovementCount,
        submission.PendingExpenseCount,
        submission.ReplenishmentDueAmount,
        submission.ReplenishmentPaidAmount,
        submission.DifferenceAmount,
        submission.PaymentReference,
        submission.CouncilFinancialReviewId,
        submission.Status,
        submission.SourceReference,
        submission.CreatedAtUtc,
        submission.SubmittedAtUtc,
        submission.ReviewedAtUtc,
        submission.ReviewNotes
    };

    private static DateOnly FirstDayOfCurrentMonthInChile()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var chileNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return new DateOnly(chileNow.Year, chileNow.Month, 1);
    }

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record HospitalariaPeriodTotals(
        decimal IncomeAmount,
        decimal ApprovedExpenseAmount,
        int MovementCount,
        int PendingExpenseCount);
}

public static class HospitalariaMovementCodes
{
    public const string Income = "income";
    public const string Expense = "expense";

    public const string DeathReplenishment = "death_replenishment";
    public const string AnnualFund = "annual_replenishment_fund";
    public const string CharityBag = "charity_bag";
    public const string VoluntaryContribution = "voluntary_contribution";
    public const string InitiationFee = "initiation_fee";
    public const string CharityAid = "charity_aid";
    public const string Supplies = "supplies";
    public const string Ceremony = "ceremony";

    public const string PendingApproval = "pending_approval";
    public const string Approved = "approved";
    public const string NotRequired = "not_required";

    public static bool IsValidType(string value) => value is Income or Expense;
    public static bool IsValidCategory(string value)
        => value is DeathReplenishment or AnnualFund or CharityBag or VoluntaryContribution or InitiationFee or
            CharityAid or Supplies or Ceremony;
}

public sealed record CreateLodgeHospitalariaMovementRequest(
    string MovementType,
    string Category,
    decimal Amount,
    DateOnly MovementDate,
    string? MemberReference,
    string? Destination,
    string? EvidenceReference,
    string? Observation);

public sealed record CouncilHospitalariaApprovalRequest(Guid CouncilDecisionId);

public sealed record UpsertHospitalariaMonthlySubmissionRequest(
    decimal ReplenishmentDueAmount,
    decimal ReplenishmentPaidAmount,
    string? PaymentReference,
    Guid? CouncilFinancialReviewId,
    string? SourceReference);
