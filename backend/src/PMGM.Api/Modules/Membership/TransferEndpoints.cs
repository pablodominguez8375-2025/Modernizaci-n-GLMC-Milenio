using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership.Entities;
using MembershipEntity = PMGM.Api.Modules.Membership.Entities.Membership;

namespace PMGM.Api.Modules.Membership;

public static class TransferEndpoints
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/members/{memberId:guid}/transfers")
            .WithTags("Membership Transfers")
            .RequireAuthorization();

        group.MapPost("/", RequestTransferAsync);
        group.MapPost("/{transferId:guid}/approve", ApproveTransferAsync);
        group.MapPost("/{transferId:guid}/execute", ExecuteTransferAsync);

        return endpoints;
    }

    private static async Task<IResult> RequestTransferAsync(
        Guid memberId,
        RequestTransferRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var sourceMembership = await db.Memberships
            .SingleOrDefaultAsync(x => x.Id == request.SourceMembershipId && x.MemberId == memberId, cancellationToken);

        if (sourceMembership is null)
        {
            return Results.NotFound(new { message = "No se encontró la pertenencia de origen para el hermano." });
        }

        if (!access.CanManageOrganization(httpContext.User, sourceMembership.OrganizationId) &&
            !access.CanApproveTransfers(httpContext.User))
        {
            return Results.Forbid();
        }

        if (sourceMembership.OrganizationId == request.TargetOrganizationId)
        {
            return Results.BadRequest(new { message = "El Taller de destino debe ser distinto del Taller de origen." });
        }

        if (request.ProposedEffectiveDate < sourceMembership.StartDate)
        {
            return Results.BadRequest(new { message = "La fecha propuesta no puede ser anterior al inicio de la pertenencia de origen." });
        }

        var targetExists = await db.Organizations
            .AnyAsync(x => x.Id == request.TargetOrganizationId && x.Type == "workshop", cancellationToken);

        if (!targetExists)
        {
            return Results.BadRequest(new { message = "El Taller de destino no existe." });
        }

        if (request.WithdrawalRequestId is null)
            return Results.BadRequest(new { message = "El traslado sólo puede iniciarse con una Carta de Retiro Voluntario aprobada y firmada." });

        var withdrawal = await db.MemberWithdrawalRequests.SingleOrDefaultAsync(
            x => x.Id == request.WithdrawalRequestId.Value && x.MemberId == memberId &&
                 x.OriginOrganizationId == sourceMembership.OrganizationId, cancellationToken);
        if (!TransferWithdrawalPolicy.Allows(sourceMembership, withdrawal, request.ProposedEffectiveDate))
            return Results.Conflict(new { message = "La Carta de Retiro Voluntario debe estar aprobada y firmada por el Orador del Taller de origen." });

        var hasOpenTransfer = await db.MemberTransfers.AnyAsync(
            x => x.MemberId == memberId &&
                 (x.Status == MembershipCodes.TransferStatus.Requested ||
                  x.Status == MembershipCodes.TransferStatus.Approved),
            cancellationToken);

        if (hasOpenTransfer)
        {
            return Results.Conflict(new { message = "El hermano ya tiene una transferencia pendiente o aprobada." });
        }

        var transfer = new MemberTransfer
        {
            MemberId = memberId,
            SourceMembershipId = sourceMembership.Id,
            SourceOrganizationId = sourceMembership.OrganizationId,
            TargetOrganizationId = request.TargetOrganizationId,
            RequestedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ProposedEffectiveDate = request.ProposedEffectiveDate,
            Status = MembershipCodes.TransferStatus.Requested,
            Reason = request.Reason,
            EvidenceReference = withdrawal!.EvidenceReference
        };

        db.MemberTransfers.Add(transfer);
        audit.Add(httpContext, "membership.transfer.requested", nameof(MemberTransfer), transfer.Id.ToString(), sourceMembership.OrganizationId, AuditResults.Success,
            new { transfer.MemberId, transfer.SourceOrganizationId, transfer.TargetOrganizationId, transfer.ProposedEffectiveDate });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/members/{memberId}/transfers/{transfer.Id}", new
        {
            transfer.Id,
            transfer.MemberId,
            transfer.SourceOrganizationId,
            transfer.TargetOrganizationId,
            transfer.ProposedEffectiveDate,
            transfer.Status
        });
    }

    private static async Task<IResult> ApproveTransferAsync(
        Guid memberId,
        Guid transferId,
        ApproveTransferRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanApproveTransfers(httpContext.User))
        {
            return Results.Forbid();
        }

        var transfer = await db.MemberTransfers
            .SingleOrDefaultAsync(x => x.Id == transferId && x.MemberId == memberId, cancellationToken);

        if (transfer is null)
        {
            return Results.NotFound();
        }

        if (transfer.Status != MembershipCodes.TransferStatus.Requested)
        {
            return Results.Conflict(new { message = "Sólo una transferencia solicitada puede aprobarse." });
        }

        if (request.ApprovedEffectiveDate < transfer.ProposedEffectiveDate)
        {
            return Results.BadRequest(new { message = "La fecha aprobada no puede ser anterior a la fecha propuesta." });
        }

        transfer.ApprovedEffectiveDate = request.ApprovedEffectiveDate;
        transfer.Resolution = request.Resolution;
        transfer.Status = MembershipCodes.TransferStatus.Approved;

        audit.Add(httpContext, "membership.transfer.approved", nameof(MemberTransfer), transfer.Id.ToString(), transfer.SourceOrganizationId, AuditResults.Success,
            new { transfer.MemberId, transfer.SourceOrganizationId, transfer.TargetOrganizationId, transfer.ApprovedEffectiveDate });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            transfer.Id,
            transfer.Status,
            transfer.ApprovedEffectiveDate,
            transfer.Resolution
        });
    }

    private static async Task<IResult> ExecuteTransferAsync(
        Guid memberId,
        Guid transferId,
        HttpContext httpContext,
        PmgmDbContext db,
        AdmissionsDbContext admissionsDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanApproveTransfers(httpContext.User))
        {
            return Results.Forbid();
        }

        admissionsDb.Database.SetDbConnection(db.Database.GetDbConnection());
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        await admissionsDb.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

        var transfer = await db.MemberTransfers
            .Include(x => x.SourceMembership)
            .SingleOrDefaultAsync(x => x.Id == transferId && x.MemberId == memberId, cancellationToken);

        if (transfer is null)
        {
            return Results.NotFound();
        }

        if (transfer.Status == MembershipCodes.TransferStatus.Executed && transfer.TargetMembershipId is not null && transfer.ExecutedAtUtc is not null)
            return Results.Ok(new { transfer.Id, transfer.Status, transfer.SourceMembershipId,
                targetMembershipId = transfer.TargetMembershipId, transfer.ExecutedAtUtc, idempotent = true });

        if (transfer.Status != MembershipCodes.TransferStatus.Approved ||
            transfer.ApprovedEffectiveDate is null)
        {
            return Results.Conflict(new { message = "La transferencia debe estar aprobada antes de ejecutarse." });
        }

        if (transfer.TargetMembershipId is not null || transfer.ExecutedAtUtc is not null)
        {
            return Results.Conflict(new { message = "La transferencia ya fue ejecutada." });
        }

        var sourceMembership = transfer.SourceMembership;
        var effectiveDate = transfer.ApprovedEffectiveDate.Value;
        var withdrawal = await db.MemberWithdrawalRequests.AsNoTracking().Where(x => x.MemberId == memberId &&
            x.OriginOrganizationId == transfer.SourceOrganizationId && x.EvidenceReference == transfer.EvidenceReference &&
            x.RequestedEffectiveDate == sourceMembership.EndDate).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (!TransferWithdrawalPolicy.Allows(sourceMembership, withdrawal, effectiveDate))
            return Results.Conflict(new { message = "El origen debe conservar el cierre por retiro voluntario aprobado y firmado, anterior al destino." });
        if (effectiveDate <= sourceMembership.StartDate)
        {
            return Results.BadRequest(new { message = "La transferencia debe ser posterior al inicio de la pertenencia de origen." });
        }

        var resolvedCases = await admissionsDb.AdmissionCases.AsNoTracking().Include(x => x.Decisions)
            .Where(x => x.MemberId == memberId && x.OrganizationId == transfer.TargetOrganizationId &&
                x.AdmissionType == "affiliation" && x.Status == "resolved").ToListAsync(cancellationToken);
        var receipts = new List<AdmissionMaterialization.Receipt>();
        foreach (var admission in resolvedCases)
            foreach (var decision in admission.Decisions.Where(x => x.DecisionType == AdmissionWorkflowCodes.DecisionType.MembershipMaterialized))
            {
                try
                {
                    var receipt = JsonSerializer.Deserialize<AdmissionMaterialization.Receipt>(decision.Notes ?? "null");
                    if (receipt is not null && receipt.MemberId == memberId && receipt.EffectiveDate == effectiveDate) receipts.Add(receipt);
                }
                catch (JsonException) { /* Recibo legado no verificable: no crea ni infiere una pertenencia. */ }
            }
        if (receipts.Count != 1)
            return Results.Conflict(new { message = "El destino requiere un único expediente de afiliación resuelto y materializado con ceremonia autorizada y Tenida cerrada." });
        var targetMembership = await db.Memberships.SingleOrDefaultAsync(x => x.Id == receipts[0].MembershipId &&
            x.MemberId == memberId && x.OrganizationId == transfer.TargetOrganizationId && x.StartDate == effectiveDate &&
            x.Status == MembershipCodes.MembershipStatus.Active && x.EndDate == null, cancellationToken);
        if (targetMembership is null)
            return Results.Conflict(new { message = "No existe una pertenencia vigente que coincida con el recibo de afiliación del destino." });
        if (await db.MemberTransfers.AnyAsync(x => x.Id != transferId && x.TargetMembershipId == targetMembership.Id, cancellationToken))
            return Results.Conflict(new { message = "La pertenencia de destino ya está vinculada a otro traslado." });
        // El retiro conserva su cierre; la afiliación creó el destino. El traslado enlaza ambos segmentos.

        transfer.TargetMembership = targetMembership;
        transfer.Status = MembershipCodes.TransferStatus.Executed;
        transfer.ExecutedAtUtc = DateTimeOffset.UtcNow;

        db.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent
        {
            MemberId = memberId,
            EventType = MembershipCodes.InstitutionalStatus.WorkshopTransfer,
            EffectiveDate = effectiveDate,
            OrganizationId = transfer.TargetOrganizationId,
            Reason = transfer.Resolution ?? transfer.Reason,
            EvidenceReference = transfer.EvidenceReference,
            Notes = $"Transferencia desde {transfer.SourceOrganizationId} hacia {transfer.TargetOrganizationId}."
        });

        audit.Add(httpContext, "membership.transfer.executed", nameof(MemberTransfer), transfer.Id.ToString(), transfer.SourceOrganizationId, AuditResults.Success,
            new
            {
                transfer.MemberId,
                transfer.SourceOrganizationId,
                transfer.TargetOrganizationId,
                effectiveDate,
                sourceMembershipId = sourceMembership.Id,
                targetMembershipId = targetMembership.Id
            });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new
        {
            transfer.Id,
            transfer.Status,
            transfer.SourceMembershipId,
            sourceEndDate = sourceMembership.EndDate,
            targetMembershipId = targetMembership.Id,
            targetMembership.StartDate,
            transfer.ExecutedAtUtc
        });
    }
}

public sealed record RequestTransferRequest(
    Guid SourceMembershipId,
    Guid TargetOrganizationId,
    DateOnly ProposedEffectiveDate,
    string? Reason,
    string? EvidenceReference,
    Guid? WithdrawalRequestId = null);

public sealed record ApproveTransferRequest(
    DateOnly ApprovedEffectiveDate,
    string Resolution);
