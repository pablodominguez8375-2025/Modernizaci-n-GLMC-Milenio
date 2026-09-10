using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Treasury;

public static class MemberTreasuryEndpoints
{
    public static IEndpointRouteBuilder MapMemberTreasuryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tesoreria/talleres/{organizationId:guid}/miembros/{memberId:guid}")
            .WithTags("Tesorería Logial")
            .RequireAuthorization();

        group.MapGet("/estado-cuenta", GetStatementAsync);
        group.MapPost("/cargos", CreateChargeAsync);
        group.MapPost("/pagos", CreatePaymentAsync);

        return endpoints;
    }

    private static async Task<IResult> GetStatementAsync(
        Guid organizationId,
        Guid memberId,
        HttpContext httpContext,
        PmgmDbContext db,
        TreasuryLedgerDbContext ledger,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!CanManageMemberLedger(httpContext.User, access, organizationId))
            return Results.Forbid();

        if (!await HasMembershipAsync(db, organizationId, memberId, cancellationToken))
            return Results.NotFound(new { message = "El Hermano no registra pertenencia al Taller indicado." });

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(await MemberTreasuryStatementProjection.BuildAsync(
            ledger, organizationId, memberId, cancellationToken));
    }

    private static async Task<IResult> CreateChargeAsync(
        Guid organizationId,
        Guid memberId,
        CreateMemberChargeRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        TreasuryLedgerDbContext ledger,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!CanManageMemberLedger(httpContext.User, access, organizationId))
            return Results.Forbid();

        if (!await HasMembershipAsync(db, organizationId, memberId, cancellationToken))
            return Results.NotFound(new { message = "El Hermano no registra pertenencia al Taller indicado." });

        var concept = Normalize(request.Concept);
        var period = Normalize(request.Period);
        var currency = NormalizeCurrency(request.Currency);
        if (concept is null || concept.Length > 240)
            return Results.BadRequest(new { message = "El concepto del cargo es obligatorio y no puede superar 240 caracteres." });
        if (period is { Length: > 40 })
            return Results.BadRequest(new { message = "El período no puede superar 40 caracteres." });
        if (request.Amount <= 0)
            return Results.BadRequest(new { message = "El monto del cargo debe ser mayor que cero." });
        if (!MemberTreasuryCodes.Currency.IsValid(currency))
            return Results.BadRequest(new { message = "Para este incremento la moneda autorizada es CLP." });
        if (request.DueDate < request.IssuedDate)
            return Results.BadRequest(new { message = "La fecha de vencimiento no puede ser anterior a la fecha de emisión." });
        if (Normalize(request.SourceReference) is { Length: > 500 } || Normalize(request.Notes) is { Length: > 2000 })
            return Results.BadRequest(new { message = "La referencia u observación supera el máximo permitido." });

        var charge = new MemberCharge
        {
            OrganizationId = organizationId,
            MemberId = memberId,
            Concept = concept,
            Period = period,
            IssuedDate = request.IssuedDate,
            DueDate = request.DueDate,
            Amount = request.Amount,
            Currency = currency,
            Status = MemberTreasuryCodes.ChargeStatus.Open,
            SourceReference = Normalize(request.SourceReference),
            Notes = Normalize(request.Notes),
            CreatedBySubject = ActorSubject(httpContext.User)
        };

        ledger.MemberCharges.Add(charge);
        await ledger.SaveChangesAsync(cancellationToken);

        audit.Add(
            httpContext,
            "treasury.member_charge.created",
            nameof(MemberCharge),
            charge.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { charge.MemberId, charge.IssuedDate, charge.DueDate, charge.Amount, charge.Currency, charge.Status });
        await db.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Created(
            $"/api/tesoreria/talleres/{organizationId}/miembros/{memberId}/cargos/{charge.Id}",
            new
            {
                charge.Id,
                charge.OrganizationId,
                charge.MemberId,
                charge.Concept,
                charge.Period,
                charge.IssuedDate,
                charge.DueDate,
                charge.Amount,
                charge.Currency,
                charge.Status,
                charge.CreatedAtUtc
            });
    }

    private static async Task<IResult> CreatePaymentAsync(
        Guid organizationId,
        Guid memberId,
        CreateMemberPaymentRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        TreasuryLedgerDbContext ledger,
        DocumentManagementDbContext documentDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!CanManageMemberLedger(httpContext.User, access, organizationId))
            return Results.Forbid();

        if (!await HasMembershipAsync(db, organizationId, memberId, cancellationToken))
            return Results.NotFound(new { message = "El Hermano no registra pertenencia al Taller indicado." });

        var currency = NormalizeCurrency(request.Currency);
        var method = Normalize(request.Method)?.ToLowerInvariant();
        var receiptNumber = Normalize(request.ReceiptNumber);
        if (request.Amount <= 0)
            return Results.BadRequest(new { message = "El monto del pago debe ser mayor que cero." });
        if (!MemberTreasuryCodes.Currency.IsValid(currency))
            return Results.BadRequest(new { message = "Para este incremento la moneda autorizada es CLP." });
        if (method is null || !MemberTreasuryCodes.PaymentMethod.IsValid(method))
            return Results.BadRequest(new { message = "El medio de pago indicado no es válido." });
        if (receiptNumber is { Length: > 120 })
            return Results.BadRequest(new { message = "El número de comprobante no puede superar 120 caracteres." });
        if (Normalize(request.SourceReference) is { Length: > 500 } || Normalize(request.Notes) is { Length: > 2000 })
            return Results.BadRequest(new { message = "La referencia u observación supera el máximo permitido." });

        if (request.ReceiptDocumentId is not null)
        {
            var receiptExists = await documentDb.InstitutionalDocuments
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.ReceiptDocumentId.Value && x.OrganizationId == organizationId,
                    cancellationToken);
            if (!receiptExists)
                return Results.BadRequest(new { message = "El documento de comprobante no pertenece al Taller o no existe." });
        }

        var requestedAllocations = (request.Allocations ?? Array.Empty<MemberPaymentAllocationRequest>())
            .GroupBy(x => x.ChargeId)
            .Select(x => new MemberPaymentAllocationRequest(x.Key, x.Sum(item => item.Amount)))
            .ToList();

        if (requestedAllocations.Any(x => x.Amount <= 0))
            return Results.BadRequest(new { message = "Cada abono aplicado a un cargo debe ser mayor que cero." });
        if (requestedAllocations.Sum(x => x.Amount) > request.Amount)
            return Results.BadRequest(new { message = "La suma de los abonos no puede superar el monto total del pago." });

        var chargeIds = requestedAllocations.Select(x => x.ChargeId).ToArray();
        var charges = chargeIds.Length == 0
            ? new List<MemberCharge>()
            : await ledger.MemberCharges
                .Where(x => chargeIds.Contains(x.Id) && x.OrganizationId == organizationId && x.MemberId == memberId)
                .ToListAsync(cancellationToken);

        if (charges.Count != chargeIds.Length)
            return Results.BadRequest(new { message = "Uno o más cargos no pertenecen al estado de cuenta indicado." });
        if (charges.Any(x => x.Status == MemberTreasuryCodes.ChargeStatus.Void))
            return Results.BadRequest(new { message = "No se puede aplicar un pago a un cargo anulado." });
        if (charges.Any(x => !string.Equals(x.Currency, currency, StringComparison.OrdinalIgnoreCase)))
            return Results.BadRequest(new { message = "La moneda del pago debe coincidir con la moneda de todos los cargos abonados." });

        var previousAllocated = chargeIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await ledger.MemberPaymentAllocations
                .Where(x => chargeIds.Contains(x.ChargeId))
                .GroupBy(x => x.ChargeId)
                .Select(group => new { ChargeId = group.Key, Amount = group.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.ChargeId, x => x.Amount, cancellationToken);

        foreach (var allocation in requestedAllocations)
        {
            var charge = charges.Single(x => x.Id == allocation.ChargeId);
            var alreadyApplied = previousAllocated.GetValueOrDefault(charge.Id);
            if (alreadyApplied + allocation.Amount > charge.Amount)
                return Results.BadRequest(new { message = $"El abono supera el saldo pendiente del cargo {charge.Id}." });
        }

        await using var transaction = await ledger.Database.BeginTransactionAsync(cancellationToken);
        var payment = new MemberPayment
        {
            OrganizationId = organizationId,
            MemberId = memberId,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            Currency = currency,
            Method = method,
            ReceiptNumber = receiptNumber,
            ReceiptDocumentId = request.ReceiptDocumentId,
            SourceReference = Normalize(request.SourceReference),
            Notes = Normalize(request.Notes),
            RecordedBySubject = ActorSubject(httpContext.User)
        };
        ledger.MemberPayments.Add(payment);

        foreach (var allocation in requestedAllocations)
        {
            ledger.MemberPaymentAllocations.Add(new MemberPaymentAllocation
            {
                PaymentId = payment.Id,
                ChargeId = allocation.ChargeId,
                Amount = allocation.Amount
            });

            var charge = charges.Single(x => x.Id == allocation.ChargeId);
            var newApplied = previousAllocated.GetValueOrDefault(charge.Id) + allocation.Amount;
            if (newApplied == charge.Amount)
                charge.Status = MemberTreasuryCodes.ChargeStatus.Paid;
        }

        await ledger.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        audit.Add(
            httpContext,
            "treasury.member_payment.recorded",
            nameof(MemberPayment),
            payment.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new
            {
                payment.MemberId,
                payment.PaymentDate,
                payment.Amount,
                payment.Currency,
                payment.Method,
                AllocatedAmount = requestedAllocations.Sum(x => x.Amount),
                HasReceiptDocument = payment.ReceiptDocumentId is not null
            });
        await db.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Created(
            $"/api/tesoreria/talleres/{organizationId}/miembros/{memberId}/pagos/{payment.Id}",
            new
            {
                payment.Id,
                payment.OrganizationId,
                payment.MemberId,
                payment.PaymentDate,
                payment.Amount,
                payment.Currency,
                payment.Method,
                payment.ReceiptNumber,
                receiptAvailable = payment.ReceiptDocumentId is not null,
                allocatedAmount = requestedAllocations.Sum(x => x.Amount),
                unappliedAmount = payment.Amount - requestedAllocations.Sum(x => x.Amount),
                payment.RecordedAtUtc
            });
    }

    internal static bool CanManageMemberLedger(
        ClaimsPrincipal user,
        IInstitutionalAccessService access,
        Guid organizationId)
    {
        if (access.CanManageTreasuryRegularity(user)) return true;
        return access.CanReadOrganization(user, organizationId) &&
               access.HasRole(user, InstitutionalRoles.TallerAdmin, MemberTreasuryCodes.LodgeTreasuryRole);
    }

    private static Task<bool> HasMembershipAsync(
        PmgmDbContext db,
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken)
        => db.Memberships.AsNoTracking().AnyAsync(
            x => x.OrganizationId == organizationId && x.MemberId == memberId,
            cancellationToken);

    private static string ActorSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub")
           ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? "institutional-user";

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeCurrency(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? MemberTreasuryCodes.Currency.Clp
            : value.Trim().ToUpperInvariant();
}

public static class MemberTreasuryStatementProjection
{
    public static async Task<MemberTreasuryStatementDto> BuildAsync(
        TreasuryLedgerDbContext ledger,
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var charges = await ledger.MemberCharges
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.MemberId == memberId)
            .OrderByDescending(x => x.IssuedDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var payments = await ledger.MemberPayments
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.MemberId == memberId)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .ToListAsync(cancellationToken);

        var paymentIds = payments.Select(x => x.Id).ToArray();
        var allocations = paymentIds.Length == 0
            ? new List<MemberPaymentAllocation>()
            : await ledger.MemberPaymentAllocations
                .AsNoTracking()
                .Where(x => paymentIds.Contains(x.PaymentId))
                .ToListAsync(cancellationToken);

        var appliedByCharge = allocations
            .GroupBy(x => x.ChargeId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));
        var allocatedByPayment = allocations
            .GroupBy(x => x.PaymentId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));

        var chargeItems = charges.Select(charge =>
        {
            var applied = appliedByCharge.GetValueOrDefault(charge.Id);
            var outstanding = charge.Status == MemberTreasuryCodes.ChargeStatus.Void ||
                              charge.Status == MemberTreasuryCodes.ChargeStatus.Paid
                ? 0m
                : Math.Max(0m, charge.Amount - applied);
            return new MemberTreasuryChargeDto(
                charge.Id,
                charge.Concept,
                charge.Period,
                charge.IssuedDate,
                charge.DueDate,
                charge.Amount,
                applied,
                outstanding,
                charge.Currency,
                charge.Status);
        }).ToList();

        var paymentItems = payments.Select(payment =>
        {
            var allocated = allocatedByPayment.GetValueOrDefault(payment.Id);
            return new MemberTreasuryPaymentDto(
                payment.Id,
                payment.PaymentDate,
                payment.Amount,
                allocated,
                Math.Max(0m, payment.Amount - allocated),
                payment.Currency,
                payment.Method,
                payment.ReceiptNumber,
                payment.ReceiptDocumentId is not null);
        }).ToList();

        var activeCharges = chargeItems.Where(x => x.Status != MemberTreasuryCodes.ChargeStatus.Void).ToList();
        var totalCharges = activeCharges.Sum(x => x.Amount);
        var totalPayments = paymentItems.Sum(x => x.Amount);
        var totalAllocated = paymentItems.Sum(x => x.AllocatedAmount);
        var outstandingCharges = activeCharges.Sum(x => x.OutstandingAmount);
        var availableCredit = Math.Max(0m, totalPayments - totalAllocated);
        var netBalance = outstandingCharges - availableCredit;
        var overdueCount = activeCharges.Count(x => x.OutstandingAmount > 0 && x.DueDate < TodayInChile());

        return new MemberTreasuryStatementDto(
            organizationId,
            memberId,
            MemberTreasuryCodes.Currency.Clp,
            new MemberTreasurySummaryDto(
                totalCharges,
                totalPayments,
                totalAllocated,
                outstandingCharges,
                availableCredit,
                netBalance,
                activeCharges.Count(x => x.OutstandingAmount > 0),
                overdueCount),
            chargeItems,
            paymentItems);
    }

    private static DateOnly TodayInChile()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var chileNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return DateOnly.FromDateTime(chileNow.DateTime);
    }
}

public sealed record CreateMemberChargeRequest(
    string Concept,
    string? Period,
    DateOnly IssuedDate,
    DateOnly DueDate,
    decimal Amount,
    string? Currency,
    string? SourceReference,
    string? Notes);

public sealed record MemberPaymentAllocationRequest(Guid ChargeId, decimal Amount);

public sealed record CreateMemberPaymentRequest(
    DateOnly PaymentDate,
    decimal Amount,
    string? Currency,
    string Method,
    string? ReceiptNumber,
    Guid? ReceiptDocumentId,
    IReadOnlyCollection<MemberPaymentAllocationRequest>? Allocations,
    string? SourceReference,
    string? Notes);

public sealed record MemberTreasuryStatementDto(
    Guid OrganizationId,
    Guid MemberId,
    string Currency,
    MemberTreasurySummaryDto Summary,
    IReadOnlyCollection<MemberTreasuryChargeDto> Charges,
    IReadOnlyCollection<MemberTreasuryPaymentDto> Payments);

public sealed record MemberTreasurySummaryDto(
    decimal TotalCharges,
    decimal TotalPayments,
    decimal TotalAllocated,
    decimal OutstandingCharges,
    decimal AvailableCredit,
    decimal NetBalance,
    int PendingChargeCount,
    int OverdueChargeCount);

public sealed record MemberTreasuryChargeDto(
    Guid Id,
    string Concept,
    string? Period,
    DateOnly IssuedDate,
    DateOnly DueDate,
    decimal Amount,
    decimal AppliedAmount,
    decimal OutstandingAmount,
    string Currency,
    string Status);

public sealed record MemberTreasuryPaymentDto(
    Guid Id,
    DateOnly PaymentDate,
    decimal Amount,
    decimal AllocatedAmount,
    decimal UnappliedAmount,
    string Currency,
    string Method,
    string? ReceiptNumber,
    bool ReceiptAvailable);