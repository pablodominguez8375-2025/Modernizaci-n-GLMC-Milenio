using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Membership;

public static class WithdrawalEndpoints
{
    public static IEndpointRouteBuilder MapWithdrawalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/gestion-logial/retiros")
            .WithTags("Gestión Logial - Retiros")
            .RequireAuthorization();

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapPost("/{requestId:guid}/decision", DecideAsync);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateWithdrawalRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageOrganization(httpContext.User, request.OrganizationId)) return Results.Forbid();
        if (!MembershipCodes.WithdrawalType.IsValid(request.WithdrawalType))
            return Results.BadRequest(new { message = "El tipo de retiro debe ser voluntario o forzoso." });
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 10 or > 1000)
            return Results.BadRequest(new { message = "La causal o fundamento debe contener entre 10 y 1000 caracteres." });
        if (string.IsNullOrWhiteSpace(request.EvidenceReference) || request.EvidenceReference.Trim().Length is < 3 or > 500)
            return Results.BadRequest(new { message = "Debe indicar la referencia de la carta o antecedente oficial." });

        var activeMembership = await db.Memberships.AsNoTracking().OrderByDescending(x => x.StartDate).FirstOrDefaultAsync(
            x => x.MemberId == request.MemberId && x.OrganizationId == request.OrganizationId &&
                 x.Status == MembershipCodes.MembershipStatus.Active && x.EndDate == null,
            cancellationToken);
        if (activeMembership is null)
            return Results.Conflict(new { message = "El hermano no posee una pertenencia activa en este Taller." });
        if (request.RequestedEffectiveDate < activeMembership.StartDate)
            return Results.BadRequest(new { message = "La fecha efectiva no puede ser anterior al inicio de la pertenencia." });

        var hasPending = await db.MemberWithdrawalRequests.AnyAsync(
            x => x.MemberId == request.MemberId && x.Status == MembershipCodes.WithdrawalRequestStatus.Pending,
            cancellationToken);
        if (hasPending) return Results.Conflict(new { message = "El hermano ya tiene un retiro pendiente de resolución." });

        var withdrawal = new MemberWithdrawalRequest
        {
            MemberId = request.MemberId,
            OriginOrganizationId = request.OrganizationId,
            WithdrawalType = request.WithdrawalType,
            RequestedEffectiveDate = request.RequestedEffectiveDate,
            Reason = request.Reason.Trim(),
            EvidenceReference = request.EvidenceReference.Trim(),
            Status = MembershipCodes.WithdrawalRequestStatus.Pending,
            RequestedBySubject = Subject(httpContext.User)
        };
        db.MemberWithdrawalRequests.Add(withdrawal);
        audit.Add(httpContext, "membership.withdrawal.requested", nameof(MemberWithdrawalRequest), withdrawal.Id.ToString(), request.OrganizationId,
            AuditResults.Success, new { withdrawal.MemberId, withdrawal.WithdrawalType, withdrawal.RequestedEffectiveDate });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gestion-logial/retiros/{withdrawal.Id}", ToDto(withdrawal));
    }

    private static async Task<IResult> ListAsync(
        Guid? organizationId,
        string? status,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var canReviewOrder = access.CanRunRegimenInteriorReports(httpContext.User) || access.CanManageGrandSecretariat(httpContext.User);
        if (organizationId is null && !canReviewOrder) return Results.Forbid();
        if (organizationId is not null && !canReviewOrder && !access.CanManageOrganization(httpContext.User, organizationId.Value)) return Results.Forbid();

        var query = db.MemberWithdrawalRequests.AsNoTracking().AsQueryable();
        if (organizationId is not null) query = query.Where(x => x.OriginOrganizationId == organizationId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Take(250).Select(x => new
        {
            x.Id, x.MemberId, x.OriginOrganizationId, x.WithdrawalType, x.RequestedEffectiveDate,
            x.Reason, x.EvidenceReference, x.Status, x.Resolution, x.CreatedAtUtc, x.DecidedAtUtc
        }).ToListAsync(cancellationToken);
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> DecideAsync(
        Guid requestId,
        WithdrawalDecisionRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanRunRegimenInteriorReports(httpContext.User) && !access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();
        if (request.Decision is not MembershipCodes.WithdrawalRequestStatus.Approved and not MembershipCodes.WithdrawalRequestStatus.Rejected)
            return Results.BadRequest(new { message = "La decisión debe ser approved o rejected." });
        if (string.IsNullOrWhiteSpace(request.Resolution) || request.Resolution.Trim().Length is < 10 or > 2000)
            return Results.BadRequest(new { message = "La resolución debe contener entre 10 y 2000 caracteres." });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var withdrawal = await db.MemberWithdrawalRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (withdrawal is null) return Results.NotFound();
        if (withdrawal.Status != MembershipCodes.WithdrawalRequestStatus.Pending)
            return Results.Conflict(new { message = "Este retiro ya fue resuelto." });

        withdrawal.Status = request.Decision;
        withdrawal.Resolution = request.Resolution.Trim();
        withdrawal.DecidedAtUtc = DateTimeOffset.UtcNow;
        withdrawal.DecidedBySubject = Subject(httpContext.User);

        if (request.Decision == MembershipCodes.WithdrawalRequestStatus.Approved)
        {
            var memberships = await db.Memberships.Where(x => x.MemberId == withdrawal.MemberId && x.Status == MembershipCodes.MembershipStatus.Active && x.EndDate == null).ToListAsync(cancellationToken);
            if (memberships.Any(x => x.StartDate > withdrawal.RequestedEffectiveDate))
                return Results.Conflict(new { message = "Existe una pertenencia activa iniciada después de la fecha efectiva solicitada." });
            foreach (var membership in memberships)
            {
                membership.EndDate = withdrawal.RequestedEffectiveDate;
                membership.Status = MembershipCodes.MembershipStatus.Closed;
                membership.EndReason = withdrawal.WithdrawalType == MembershipCodes.WithdrawalType.Voluntary ? "Retiro voluntario — sueño" : "Retiro forzoso — inhabilitación en la Orden";
                membership.EvidenceReference = withdrawal.EvidenceReference;
            }

            db.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent
            {
                MemberId = withdrawal.MemberId,
                OrganizationId = withdrawal.OriginOrganizationId,
                EventType = withdrawal.WithdrawalType == MembershipCodes.WithdrawalType.Voluntary
                    ? MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal
                    : MembershipCodes.InstitutionalStatus.ForcedWithdrawal,
                EffectiveDate = withdrawal.RequestedEffectiveDate,
                Reason = withdrawal.Reason,
                EvidenceReference = withdrawal.EvidenceReference,
                Notes = withdrawal.Resolution
            });
        }

        audit.Add(httpContext, $"membership.withdrawal.{request.Decision}", nameof(MemberWithdrawalRequest), withdrawal.Id.ToString(), withdrawal.OriginOrganizationId,
            AuditResults.Success, new { withdrawal.MemberId, withdrawal.WithdrawalType, withdrawal.Status, withdrawal.RequestedEffectiveDate });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(ToDto(withdrawal));
    }

    private static object ToDto(MemberWithdrawalRequest value) => new
    {
        value.Id, value.MemberId, value.OriginOrganizationId, value.WithdrawalType,
        value.RequestedEffectiveDate, value.Status, value.Resolution, value.CreatedAtUtc, value.DecidedAtUtc
    };

    private static string Subject(ClaimsPrincipal user) => user.FindFirst("sub")?.Value ?? "unknown";
}

public sealed record CreateWithdrawalRequest(Guid MemberId, Guid OrganizationId, string WithdrawalType, DateOnly RequestedEffectiveDate, string Reason, string EvidenceReference);
public sealed record WithdrawalDecisionRequest(string Decision, string Resolution);
