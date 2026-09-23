using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Hospitalaria;

public static class HospitalariaEndpoints
{
    public static IEndpointRouteBuilder MapHospitalariaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/hospitalaria")
            .WithTags("Gran Hospitalaria")
            .RequireAuthorization();

        group.MapPost("/talleres/{organizationId:guid}/regularidad", SetWorkshopRegularityAsync);
        group.MapGet("/talleres/{organizationId:guid}/regularidad", GetWorkshopRegularityAsync);
        group.MapGet("/rendiciones", ListMonthlySubmissionsAsync);
        group.MapPost("/rendiciones/{submissionId:guid}/revision", ReviewMonthlySubmissionAsync);
        group.MapGet("/reposiciones/tarifa", GetReplenishmentRateAsync);
        group.MapPost("/reposiciones/tarifa", SetReplenishmentRateAsync);
        group.MapPost("/reposiciones/sincronizar-defunciones", SyncDeathReplenishmentsAsync);
        group.MapGet("/reposiciones", ListDeathReplenishmentCasesAsync);
        group.MapGet("/talleres/{organizationId:guid}/reposiciones", ListWorkshopReplenishmentObligationsAsync);
        group.MapPost("/reposiciones/obligaciones/{obligationId:guid}/pagos", RecordReplenishmentPaymentAsync);
        group.MapPost("/reposiciones/casos/{caseId:guid}/talleres/{organizationId:guid}/transferencias", SubmitReplenishmentTransferAsync);
        group.MapPost("/reposiciones/transferencias/{transferId:guid}/revision", ReviewReplenishmentTransferAsync);

        return endpoints;
    }

    private static async Task<IResult> GetReplenishmentRateAsync(
        DateOnly? asOf, HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageHospitalariaRegularity(context.User)) return Results.Forbid();
        var date = asOf ?? TodayInChile();
        var rate = await db.HospitalariaReplenishmentRates.AsNoTracking()
            .Where(x => x.EffectiveFrom <= date && (x.EffectiveUntil == null || x.EffectiveUntil >= date))
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        return rate is null ? Results.NotFound(new { message = "No existe una tarifa vigente de reposición." }) : Results.Ok(rate);
    }

    private static async Task<IResult> SetReplenishmentRateAsync(
        HospitalariaRateRequest request, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageHospitalariaRegularity(context.User)) return Results.Forbid();
        if (request.AmountPerActiveMember <= 0 || string.IsNullOrWhiteSpace(request.SourceReference))
            return Results.BadRequest(new { message = "La tarifa debe ser positiva e indicar la referencia institucional de vigencia." });
        var next = await db.HospitalariaReplenishmentRates.Where(x => x.EffectiveFrom < request.EffectiveFrom &&
                (x.EffectiveUntil == null || x.EffectiveUntil >= request.EffectiveFrom))
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
        var overlap = await db.HospitalariaReplenishmentRates.AnyAsync(x => x.EffectiveFrom >= request.EffectiveFrom &&
            (request.EffectiveUntil == null || x.EffectiveFrom <= request.EffectiveUntil), ct);
        if (overlap || request.EffectiveUntil < request.EffectiveFrom)
            return Results.Conflict(new { message = "La vigencia de la tarifa se superpone con otra configuración." });
        if (next is not null) next.EffectiveUntil = request.EffectiveFrom.AddDays(-1);
        var rate = new HospitalariaReplenishmentRate {
            AmountPerActiveMember = request.AmountPerActiveMember,
            EffectiveFrom = request.EffectiveFrom, EffectiveUntil = request.EffectiveUntil,
            SourceReference = request.SourceReference.Trim(), CreatedBySubject = GetSubject(context.User)
        };
        db.HospitalariaReplenishmentRates.Add(rate);
        audit.Add(context, "hospitalaria.replenishment_rate.created", nameof(HospitalariaReplenishmentRate), rate.Id.ToString(),
            null, AuditResults.Success, new { rate.AmountPerActiveMember, rate.EffectiveFrom, rate.EffectiveUntil, rate.SourceReference });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/hospitalaria/reposiciones/tarifa?asOf={rate.EffectiveFrom:yyyy-MM-dd}", rate);
    }

    // Gran Hospitalaria procesa de forma idempotente las defunciones nuevas al abrir su bandeja.
    private static async Task<IResult> SyncDeathReplenishmentsAsync(
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageHospitalariaRegularity(context.User)) return Results.Forbid();
        var events = await db.InstitutionalStatusEvents.AsNoTracking()
            .Where(x => x.EventType == MembershipCodes.InstitutionalStatus.Deceased &&
                        !db.DeathReplenishmentCases.Any(c => c.DeathStatusEventId == x.Id))
            .OrderBy(x => x.EffectiveDate).ThenBy(x => x.RecordedAtUtc).Take(250).ToListAsync(ct);
        var created = 0;
        foreach (var death in events)
        {
            var rate = await db.HospitalariaReplenishmentRates.AsNoTracking()
                .Where(x => x.EffectiveFrom <= death.EffectiveDate && (x.EffectiveUntil == null || x.EffectiveUntil >= death.EffectiveDate))
                .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
            if (rate is null) continue;
            var memberships = await db.Memberships.AsNoTracking()
                .Where(x => x.MemberId != death.MemberId && x.Organization.Type == "workshop" &&
                    x.Status == MembershipCodes.MembershipStatus.Active &&
                    (x.StartDate == null || x.StartDate <= death.EffectiveDate) &&
                    (x.EndDate == null || x.EndDate >= death.EffectiveDate))
                .Select(x => new { x.Id, x.OrganizationId, x.MemberId })
                .ToListAsync(ct);
            var activeMemberships = new List<(Guid Id, Guid OrganizationId, Guid MemberId)>();
            var memberIds = memberships.Select(x => x.MemberId).Distinct().ToList();
            var latestStatuses = await db.InstitutionalStatusEvents.AsNoTracking()
                .Where(x => memberIds.Contains(x.MemberId) && x.EffectiveDate <= death.EffectiveDate)
                .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.RecordedAtUtc)
                .Select(x => new { x.MemberId, x.EventType })
                .ToListAsync(ct);
            var statusByMember = latestStatuses.GroupBy(x => x.MemberId)
                .ToDictionary(x => x.Key, x => x.First().EventType);
            foreach (var membership in memberships)
            {
                if (!statusByMember.TryGetValue(membership.MemberId, out var latestStatus) ||
                    latestStatus is MembershipCodes.InstitutionalStatus.Active or MembershipCodes.InstitutionalStatus.Reinstated)
                    activeMemberships.Add((membership.Id, membership.OrganizationId, membership.MemberId));
            }

            var replenishmentCase = new DeathReplenishmentCase {
                DeathStatusEventId = death.Id, DeceasedMemberId = death.MemberId, DeathDate = death.EffectiveDate,
                AmountPerActiveMember = rate.AmountPerActiveMember, Status = HospitalariaCodes.ReplenishmentStatus.Pending,
                CreatedBySubject = GetSubject(context.User)
            };
            foreach (var group in activeMemberships.GroupBy(x => x.OrganizationId))
                foreach (var membership in group)
                    replenishmentCase.Obligations.Add(new DeathReplenishmentObligation {
                        OrganizationId = group.Key, MembershipId = membership.Id, MemberId = membership.MemberId,
                        AmountDue = rate.AmountPerActiveMember, Status = HospitalariaCodes.ReplenishmentStatus.Pending
                    });
            db.DeathReplenishmentCases.Add(replenishmentCase);
            foreach (var organizationId in activeMemberships.Select(x => x.OrganizationId).Distinct())
                db.HospitalariaRegularitySnapshots.Add(new HospitalariaRegularitySnapshot {
                    OrganizationId = organizationId, Status = HospitalariaCodes.RegularityStatus.Overdue,
                    AsOfDate = TodayInChile(), SourceReference = $"hospitalaria-reposicion:{replenishmentCase.Id}",
                    Notes = "Nueva reposición por fallecimiento pendiente de cobro y transferencia a Gran Hospitalaria."
                });
            audit.Add(context, "hospitalaria.death_replenishment.generated", nameof(DeathReplenishmentCase),
                replenishmentCase.Id.ToString(), null, AuditResults.Success,
                new { deathStatusEventId = death.Id, death.EffectiveDate, replenishmentCase.AmountPerActiveMember, activeMembers = activeMemberships.Count });
            created++;
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { createdCases = created, message = $"Se generaron {created} casos de reposición desde defunciones registradas." });
    }

    private static async Task<IResult> ListDeathReplenishmentCasesAsync(
        HttpContext context, PmgmDbContext db, IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanManageHospitalariaRegularity(context.User)) return Results.Forbid();
        var cases = await db.DeathReplenishmentCases.AsNoTracking()
            .Include(x => x.DeceasedMember).ThenInclude(x => x.Person)
            .Include(x => x.Obligations).ThenInclude(x => x.Payments)
            .OrderByDescending(x => x.DeathDate).Take(200).ToListAsync(ct);
        var ids = cases.Select(x => x.Id).ToList();
        var transfers = await db.DeathReplenishmentTransfers.AsNoTracking().Include(x => x.Organization)
            .Where(x => ids.Contains(x.CaseId)).ToListAsync(ct);
        var items = cases.Select(x => new {
            x.Id, x.DeathDate, x.AmountPerActiveMember, x.Status,
            deceasedDisplayName = $"{x.DeceasedMember.Person.FirstNames} {x.DeceasedMember.Person.LastNames}",
            obligatedMembers = x.Obligations.Count,
            dueAmount = x.Obligations.Sum(y => y.AmountDue),
            paidAmount = x.Obligations.Sum(y => y.Payments.Sum(p => p.Amount)),
            pendingMembers = x.Obligations.Count(y => y.Status != HospitalariaCodes.ReplenishmentStatus.Paid),
            transfers = transfers.Where(t => t.CaseId == x.Id).Select(t => new {
                t.Id, t.OrganizationId, organizationName = t.Organization.Name, t.Amount,
                expectedAmount = x.Obligations.Where(o => o.OrganizationId == t.OrganizationId).Sum(o => o.AmountDue),
                t.TransferDate, t.Reference, t.Status, t.ReviewNotes
            }).ToList()
        }).ToList();
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> ListWorkshopReplenishmentObligationsAsync(
        Guid organizationId, DateOnly? asOf, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, CancellationToken ct)
    {
        if (!access.CanReadLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();
        var cutoff = asOf ?? TodayInChile();
        var obligations = await db.DeathReplenishmentObligations.AsNoTracking()
            .Include(x => x.Member).ThenInclude(x => x.Person)
            .Include(x => x.Case).ThenInclude(x => x.DeceasedMember).ThenInclude(x => x.Person)
            .Include(x => x.Payments)
            .Where(x => x.OrganizationId == organizationId && x.Case.DeathDate <= cutoff)
            .OrderByDescending(x => x.Case.DeathDate).ThenBy(x => x.Member.Person.LastNames)
            .Take(1000).ToListAsync(ct);
        var caseIds = obligations.Select(x => x.CaseId).Distinct().ToList();
        var transfers = await db.DeathReplenishmentTransfers.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && caseIds.Contains(x.CaseId)).ToListAsync(ct);
        var items = obligations.Select(x => new {
            x.Id, x.CaseId, x.Case.DeathDate, x.AmountDue,
            deceasedDisplayName = $"{x.Case.DeceasedMember.Person.FirstNames} {x.Case.DeceasedMember.Person.LastNames}",
            memberId = x.MemberId, memberDisplayName = $"{x.Member.Person.FirstNames} {x.Member.Person.LastNames}",
            paidAmount = x.Payments.Sum(p => p.Amount), balance = x.AmountDue - x.Payments.Sum(p => p.Amount), x.Status,
            payments = x.Payments.OrderBy(p => p.PaymentDate).Select(p => new { p.Id, p.Amount, p.PaymentMethod, p.PaymentDate, p.ReceiptNumber, p.Reference })
        }).ToList();
        var cases = obligations.GroupBy(x => x.CaseId).Select(group => {
            var caseObligations = group.ToList();
            var transfer = transfers.Where(x => x.CaseId == group.Key)
                .OrderByDescending(x => x.SubmissionNumber).FirstOrDefault();
            return new {
                caseId = group.Key, deathDate = caseObligations[0].Case.DeathDate,
                deceasedDisplayName = $"{caseObligations[0].Case.DeceasedMember.Person.FirstNames} {caseObligations[0].Case.DeceasedMember.Person.LastNames}",
                dueAmount = caseObligations.Sum(x => x.AmountDue),
                paidAmount = caseObligations.Sum(x => x.Payments.Sum(p => p.Amount)),
                allPaid = caseObligations.All(x => x.Status == HospitalariaCodes.ReplenishmentStatus.Paid),
                transfer = transfer is null ? null : new { transfer.Id, transfer.Amount, transfer.Reference, transfer.Status, transfer.TransferDate }
            };
        }).ToList();
        context.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { organizationId, items, cases });
    }

    private static async Task<IResult> RecordReplenishmentPaymentAsync(
        Guid obligationId, ReplenishmentPaymentRequest request, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        var obligation = await db.DeathReplenishmentObligations.Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == obligationId, ct);
        if (obligation is null) return Results.NotFound();
        if (!access.CanManageLodgeHospitalaria(context.User, obligation.OrganizationId)) return Results.Forbid();
        var paid = obligation.Payments.Sum(x => x.Amount);
        if (request.Amount <= 0 || request.Amount > obligation.AmountDue - paid ||
            string.IsNullOrWhiteSpace(request.Reference) || string.IsNullOrWhiteSpace(request.PaymentMethod))
            return Results.BadRequest(new { message = "El pago debe ser positivo, no superar el saldo y conservar medio y comprobante/referencia." });
        var reference = request.Reference.Trim();
        if (obligation.Payments.Any(x => x.Amount == request.Amount && x.PaymentDate == request.PaymentDate &&
            x.PaymentMethod == request.PaymentMethod && x.Reference == reference))
            return Results.Conflict(new { message = "El pago ya fue registrado con esa fecha, monto, medio y referencia." });
        var payment = new DeathReplenishmentPayment {
            ObligationId = obligation.Id, Amount = request.Amount, PaymentMethod = request.PaymentMethod.Trim(),
            PaymentDate = request.PaymentDate, Reference = reference,
            ReceiptNumber = $"HOSP-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            RecordedBySubject = GetSubject(context.User)
        };
        db.DeathReplenishmentPayments.Add(payment);
        obligation.Status = paid + request.Amount == obligation.AmountDue
            ? HospitalariaCodes.ReplenishmentStatus.Paid
            : HospitalariaCodes.ReplenishmentStatus.Partial;
        audit.Add(context, "lodge.hospitalaria.death_replenishment.payment_recorded", nameof(DeathReplenishmentPayment),
            payment.Id.ToString(), obligation.OrganizationId, AuditResults.Success,
            new { obligation.CaseId, obligation.MemberId, payment.Amount, payment.ReceiptNumber });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/hospitalaria/reposiciones/obligaciones/{obligation.Id}/pagos/{payment.Id}", payment);
    }

    private static async Task<IResult> SubmitReplenishmentTransferAsync(
        Guid caseId, Guid organizationId, ReplenishmentTransferRequest request, HttpContext context,
        PmgmDbContext db, IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageLodgeHospitalaria(context.User, organizationId)) return Results.Forbid();
        var caseExists = await db.DeathReplenishmentCases.AnyAsync(x => x.Id == caseId, ct);
        if (!caseExists) return Results.NotFound();
        var obligations = await db.DeathReplenishmentObligations.Include(x => x.Payments)
            .Where(x => x.CaseId == caseId && x.OrganizationId == organizationId).ToListAsync(ct);
        if (obligations.Count == 0) return Results.NotFound();
        if (obligations.Any(x => x.Status != HospitalariaCodes.ReplenishmentStatus.Paid))
            return Results.Conflict(new { message = "La transferencia se habilita cuando Hospitalaria ha cobrado todas las reposiciones del caso." });
        var previousTransfer = await db.DeathReplenishmentTransfers
            .Where(x => x.CaseId == caseId && x.OrganizationId == organizationId)
            .OrderByDescending(x => x.SubmissionNumber).FirstOrDefaultAsync(ct);
        if (previousTransfer is not null && previousTransfer.Status != HospitalariaCodes.ReplenishmentStatus.Observed)
            return Results.Conflict(new { message = "La transferencia más reciente sigue en revisión o ya fue conciliada." });
        var expected = obligations.Sum(x => x.AmountDue);
        if (request.Amount != expected || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Reference))
            return Results.BadRequest(new { message = "El monto transferido debe coincidir exactamente con el total cobrado y tener referencia." });
        var transfer = new DeathReplenishmentTransfer {
            CaseId = caseId, OrganizationId = organizationId, Amount = request.Amount,
            SubmissionNumber = (previousTransfer?.SubmissionNumber ?? 0) + 1,
            TransferDate = request.TransferDate, Reference = request.Reference.Trim(),
            Status = HospitalariaCodes.ReplenishmentStatus.Submitted, RecordedBySubject = GetSubject(context.User)
        };
        db.DeathReplenishmentTransfers.Add(transfer);
        audit.Add(context, "lodge.hospitalaria.death_replenishment.transfer_submitted", nameof(DeathReplenishmentTransfer),
            transfer.Id.ToString(), organizationId, AuditResults.Success, new { caseId, transfer.Amount, transfer.Reference });
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/hospitalaria/reposiciones/transferencias/{transfer.Id}", transfer);
    }

    private static async Task<IResult> ReviewReplenishmentTransferAsync(
        Guid transferId, ReplenishmentTransferReviewRequest request, HttpContext context, PmgmDbContext db,
        IInstitutionalAccessService access, IAuditService audit, CancellationToken ct)
    {
        if (!access.CanManageHospitalariaRegularity(context.User)) return Results.Forbid();
        if (request.Decision is not "reconciled" and not "observed" || request.Decision == "observed" && string.IsNullOrWhiteSpace(request.Notes))
            return Results.BadRequest(new { message = "La revisión debe conciliar o indicar el motivo de observación." });
        var transfer = await db.DeathReplenishmentTransfers.Include(x => x.Case)
            .SingleOrDefaultAsync(x => x.Id == transferId, ct);
        if (transfer is null) return Results.NotFound();
        if (transfer.Status != HospitalariaCodes.ReplenishmentStatus.Submitted)
            return Results.Conflict(new { message = "Sólo una transferencia enviada admite revisión." });
        var obligations = await db.DeathReplenishmentObligations.Include(x => x.Payments)
            .Where(x => x.CaseId == transfer.CaseId && x.OrganizationId == transfer.OrganizationId).ToListAsync(ct);
        var expected = obligations.Sum(x => x.AmountDue);
        var collected = obligations.Sum(x => x.Payments.Sum(p => p.Amount));
        var exact = transfer.Amount == expected && collected == expected && obligations.All(x => x.Status == HospitalariaCodes.ReplenishmentStatus.Paid);
        if (request.Decision == "reconciled" && !exact)
            return Results.Conflict(new { message = "El monto transferido debe coincidir con el total adeudado y recaudado por el Taller." });
        transfer.Status = exact && request.Decision == "reconciled"
            ? HospitalariaCodes.ReplenishmentStatus.Reconciled : HospitalariaCodes.ReplenishmentStatus.Observed;
        transfer.ReviewedBySubject = GetSubject(context.User);
        transfer.ReviewedAtUtc = DateTimeOffset.UtcNow;
        transfer.ReviewNotes = Normalize(request.Notes);

        var allObligations = await db.DeathReplenishmentObligations.Include(x => x.Payments)
            .Where(x => x.OrganizationId == transfer.OrganizationId).ToListAsync(ct);
        var allTransfers = await db.DeathReplenishmentTransfers
            .Where(x => x.OrganizationId == transfer.OrganizationId).ToListAsync(ct);
        var outstanding = allObligations.Any(x => x.Status != HospitalariaCodes.ReplenishmentStatus.Paid) ||
            allObligations.GroupBy(x => x.CaseId).Any(group => allTransfers
                .Where(t => t.CaseId == group.Key).OrderByDescending(t => t.SubmissionNumber).FirstOrDefault()?.Status
                != HospitalariaCodes.ReplenishmentStatus.Reconciled);
        db.HospitalariaRegularitySnapshots.Add(new HospitalariaRegularitySnapshot {
            OrganizationId = transfer.OrganizationId,
            Status = transfer.Status == HospitalariaCodes.ReplenishmentStatus.Reconciled && !outstanding
                ? HospitalariaCodes.RegularityStatus.UpToDate : HospitalariaCodes.RegularityStatus.Overdue,
            AsOfDate = TodayInChile(), SourceReference = $"hospitalaria-reposicion:{transfer.Id}",
            Notes = transfer.Status == HospitalariaCodes.ReplenishmentStatus.Reconciled && !outstanding
                ? "Reposiciones recaudadas y transferidas a Gran Hospitalaria; visto bueno institucional." : "Reposición observada o mantiene obligaciones/transferencias pendientes."
        });
        audit.Add(context, transfer.Status == HospitalariaCodes.ReplenishmentStatus.Reconciled
                ? "hospitalaria.death_replenishment.transfer_reconciled" : "hospitalaria.death_replenishment.transfer_observed",
            nameof(DeathReplenishmentTransfer), transfer.Id.ToString(), transfer.OrganizationId,
            transfer.Status == HospitalariaCodes.ReplenishmentStatus.Reconciled ? AuditResults.Success : AuditResults.Observed,
            new { transfer.CaseId, transfer.Amount, expected, collected, transfer.Status });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { transfer.Id, transfer.CaseId, transfer.OrganizationId, transfer.Amount, expected, collected, transfer.Status, transfer.ReviewedAtUtc });
    }

    private static async Task<IResult> SetWorkshopRegularityAsync(
        Guid organizationId,
        HospitalariaRegularityRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageHospitalariaRegularity(httpContext.User))
        {
            return Results.Forbid();
        }

        if (!HospitalariaCodes.RegularityStatus.IsValid(request.Status))
        {
            return Results.BadRequest(new { message = "El estado de regularidad hospitalaria indicado no es válido." });
        }

        var organizationExists = await db.Organizations.AnyAsync(x => x.Id == organizationId, cancellationToken);
        if (!organizationExists)
        {
            return Results.NotFound(new { message = "El Taller u organización no existe." });
        }

        var snapshot = new HospitalariaRegularitySnapshot
        {
            OrganizationId = organizationId,
            Status = request.Status,
            AsOfDate = request.AsOfDate,
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        db.HospitalariaRegularitySnapshots.Add(snapshot);
        audit.Add(
            httpContext,
            "hospitalaria.workshop_regularity.recorded",
            nameof(HospitalariaRegularitySnapshot),
            snapshot.Id.ToString(),
            organizationId,
            AuditResults.Success,
            new { snapshot.Status, snapshot.AsOfDate });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/hospitalaria/talleres/{organizationId}/regularidad",
            ToAdministrativeResponse(snapshot));
    }

    private static async Task<IResult> GetWorkshopRegularityAsync(
        Guid organizationId,
        DateOnly? asOf,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanReadInstitutionalRegularity(httpContext.User) &&
            !access.CanReadOrganization(httpContext.User, organizationId))
        {
            return Results.Forbid();
        }

        var cutoff = asOf ?? TodayInChile();
        var snapshot = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.AsOfDate <= cutoff)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot is null)
        {
            return Results.NotFound(new { message = "No existe una validación de Gran Hospitalaria para la fecha indicada." });
        }

        if (access.CanManageHospitalariaRegularity(httpContext.User))
        {
            return Results.Ok(ToAdministrativeResponse(snapshot));
        }

        return Results.Ok(ToMinimalProjection(snapshot));
    }

    private static async Task<IResult> ListMonthlySubmissionsAsync(
        Guid? organizationId,
        int? year,
        int? month,
        string? status,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageHospitalariaRegularity(httpContext.User)) return Results.Forbid();

        if (month is < 1 or > 12)
            return Results.BadRequest(new { message = "El mes indicado no es válido." });
        if (!string.IsNullOrWhiteSpace(status) && !HospitalariaCodes.SubmissionStatus.IsValid(status))
            return Results.BadRequest(new { message = "El estado de rendición no es válido." });

        var query = db.HospitalariaMonthlySubmissions
            .AsNoTracking()
            .Include(x => x.Organization)
            .Where(x => x.Status != HospitalariaCodes.SubmissionStatus.Draft);

        if (organizationId is not null) query = query.Where(x => x.OrganizationId == organizationId.Value);
        if (year is not null) query = query.Where(x => x.PeriodYear == year.Value);
        if (month is not null) query = query.Where(x => x.PeriodMonth == month.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var items = await query
            .OrderByDescending(x => x.PeriodYear)
            .ThenByDescending(x => x.PeriodMonth)
            .ThenByDescending(x => x.SubmittedAtUtc)
            .Take(250)
            .Select(x => new
            {
                x.Id,
                x.OrganizationId,
                organizationName = x.Organization.Name,
                organizationNumber = x.Organization.Number,
                x.PeriodYear,
                x.PeriodMonth,
                x.CutoffDate,
                x.IncomeAmount,
                x.ApprovedExpenseAmount,
                x.PeriodNetAmount,
                x.MovementCount,
                x.PendingExpenseCount,
                x.ReplenishmentDueAmount,
                x.ReplenishmentPaidAmount,
                x.DifferenceAmount,
                x.PaymentReference,
                x.CouncilFinancialReviewId,
                x.Status,
                x.SourceReference,
                x.SubmittedAtUtc,
                x.ReviewedAtUtc,
                x.ReviewNotes
            })
            .ToListAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> ReviewMonthlySubmissionAsync(
        Guid submissionId,
        HospitalariaSubmissionReviewRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageHospitalariaRegularity(httpContext.User)) return Results.Forbid();
        if (!HospitalariaCodes.ReviewDecision.IsValid(request.Decision))
            return Results.BadRequest(new { message = "La decisión indicada no es válida." });
        if (request.Decision == HospitalariaCodes.ReviewDecision.Observed && string.IsNullOrWhiteSpace(request.Notes))
            return Results.BadRequest(new { message = "Una observación de Gran Hospitalaria debe indicar el motivo." });

        var submission = await db.HospitalariaMonthlySubmissions
            .SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
        if (submission is null) return Results.NotFound();
        if (submission.Status != HospitalariaCodes.SubmissionStatus.Submitted)
            return Results.Conflict(new { message = "Sólo una rendición enviada puede ser revisada por Gran Hospitalaria." });

        var reviewer = GetSubject(httpContext.User);
        var now = DateTimeOffset.UtcNow;

        if (request.Decision == HospitalariaCodes.ReviewDecision.Reconciled)
        {
            if (submission.DifferenceAmount > 0)
                return Results.Conflict(new { message = "No puede conciliarse una rendición con reposiciones pendientes." });

            submission.Status = HospitalariaCodes.SubmissionStatus.Reconciled;
            submission.ReviewedBySubject = reviewer;
            submission.ReviewedAtUtc = now;
            submission.ReviewNotes = Normalize(request.Notes);

            db.HospitalariaRegularitySnapshots.Add(new HospitalariaRegularitySnapshot
            {
                OrganizationId = submission.OrganizationId,
                Status = HospitalariaCodes.RegularityStatus.UpToDate,
                AsOfDate = submission.CutoffDate,
                SourceReference = $"hospitalaria-rendicion:{submission.Id}",
                Notes = "Regularidad derivada de rendición mensual conciliada por Gran Hospitalaria."
            });

            audit.Add(
                httpContext,
                "hospitalaria.monthly_submission.reconciled",
                nameof(HospitalariaMonthlySubmission),
                submission.Id.ToString(),
                submission.OrganizationId,
                AuditResults.Success,
                new
                {
                    submission.PeriodYear,
                    submission.PeriodMonth,
                    submission.ReplenishmentDueAmount,
                    submission.ReplenishmentPaidAmount,
                    submission.DifferenceAmount
                });
        }
        else
        {
            submission.Status = HospitalariaCodes.SubmissionStatus.Observed;
            submission.ReviewedBySubject = reviewer;
            submission.ReviewedAtUtc = now;
            submission.ReviewNotes = request.Notes!.Trim();

            db.HospitalariaRegularitySnapshots.Add(new HospitalariaRegularitySnapshot
            {
                OrganizationId = submission.OrganizationId,
                Status = submission.DifferenceAmount > 0
                    ? HospitalariaCodes.RegularityStatus.Overdue
                    : HospitalariaCodes.RegularityStatus.Pending,
                AsOfDate = submission.CutoffDate,
                SourceReference = $"hospitalaria-rendicion:{submission.Id}",
                Notes = "Regularidad observada por Gran Hospitalaria; revisar la rendición agregada."
            });

            audit.Add(
                httpContext,
                "hospitalaria.monthly_submission.observed",
                nameof(HospitalariaMonthlySubmission),
                submission.Id.ToString(),
                submission.OrganizationId,
                AuditResults.Observed,
                new
                {
                    submission.PeriodYear,
                    submission.PeriodMonth,
                    submission.DifferenceAmount
                });
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            submission.Id,
            submission.OrganizationId,
            submission.Status,
            submission.ReviewedAtUtc,
            submission.ReviewNotes
        });
    }

    private static HospitalariaRegularityProjectionDto ToMinimalProjection(HospitalariaRegularitySnapshot snapshot)
        => new(snapshot.Status, snapshot.AsOfDate);

    private static object ToAdministrativeResponse(HospitalariaRegularitySnapshot snapshot) => new
    {
        snapshot.Id,
        snapshot.OrganizationId,
        snapshot.Status,
        snapshot.AsOfDate,
        snapshot.SourceReference,
        snapshot.Notes,
        snapshot.RecordedAtUtc
    };

    private static DateOnly TodayInChile()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var chileNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return DateOnly.FromDateTime(chileNow.DateTime);
    }

    private static string GetSubject(ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record HospitalariaRegularityProjectionDto(
    string Status,
    DateOnly AsOfDate);

public sealed record HospitalariaRegularityRequest(
    string Status,
    DateOnly AsOfDate,
    string? SourceReference,
    string? Notes);

public sealed record HospitalariaSubmissionReviewRequest(
    string Decision,
    string? Notes);

public sealed record HospitalariaRateRequest(decimal AmountPerActiveMember, DateOnly EffectiveFrom, DateOnly? EffectiveUntil, string SourceReference);
public sealed record ReplenishmentPaymentRequest(decimal Amount, string PaymentMethod, DateOnly PaymentDate, string Reference);
public sealed record ReplenishmentTransferRequest(decimal Amount, DateOnly TransferDate, string Reference);
public sealed record ReplenishmentTransferReviewRequest(string Decision, string? Notes);
