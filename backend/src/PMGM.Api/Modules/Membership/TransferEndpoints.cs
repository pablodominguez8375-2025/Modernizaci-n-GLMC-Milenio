using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
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

        if (sourceMembership.EndDate is not null)
        {
            return Results.Conflict(new { message = "La pertenencia de origen ya está cerrada." });
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
            .AnyAsync(x => x.Id == request.TargetOrganizationId, cancellationToken);

        if (!targetExists)
        {
            return Results.BadRequest(new { message = "El Taller de destino no existe." });
        }

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
            EvidenceReference = request.EvidenceReference
        };

        db.MemberTransfers.Add(transfer);
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
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanApproveTransfers(httpContext.User))
        {
            return Results.Forbid();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var transfer = await db.MemberTransfers
            .Include(x => x.SourceMembership)
            .SingleOrDefaultAsync(x => x.Id == transferId && x.MemberId == memberId, cancellationToken);

        if (transfer is null)
        {
            return Results.NotFound();
        }

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
        if (sourceMembership.EndDate is not null)
        {
            return Results.Conflict(new { message = "La pertenencia de origen ya se encuentra cerrada." });
        }

        var effectiveDate = transfer.ApprovedEffectiveDate.Value;
        if (effectiveDate <= sourceMembership.StartDate)
        {
            return Results.BadRequest(new { message = "La transferencia debe ser posterior al inicio de la pertenencia de origen." });
        }

        var targetMembershipExists = await db.Memberships.AnyAsync(
            x => x.MemberId == memberId &&
                 x.OrganizationId == transfer.TargetOrganizationId &&
                 x.StartDate <= effectiveDate &&
                 (x.EndDate == null || x.EndDate >= effectiveDate),
            cancellationToken);

        if (targetMembershipExists)
        {
            return Results.Conflict(new { message = "El hermano ya posee una pertenencia vigente en el Taller de destino para esa fecha." });
        }

        sourceMembership.EndDate = effectiveDate.AddDays(-1);
        sourceMembership.Status = MembershipCodes.MembershipStatus.Transferred;
        sourceMembership.EndReason = "Transferencia a otro Taller";

        var targetMembership = new MembershipEntity
        {
            MemberId = memberId,
            OrganizationId = transfer.TargetOrganizationId,
            MembershipType = sourceMembership.MembershipType,
            StartDate = effectiveDate,
            Status = MembershipCodes.MembershipStatus.Active,
            EvidenceReference = transfer.EvidenceReference
        };

        transfer.TargetMembership = targetMembership;
        transfer.Status = MembershipCodes.TransferStatus.Executed;
        transfer.ExecutedAtUtc = DateTimeOffset.UtcNow;

        db.Memberships.Add(targetMembership);
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
    string? EvidenceReference);

public sealed record ApproveTransferRequest(
    DateOnly ApprovedEffectiveDate,
    string Resolution);
