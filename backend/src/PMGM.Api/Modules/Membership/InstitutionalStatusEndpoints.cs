using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Membership;

public static class InstitutionalStatusEndpoints
{
    private static readonly HashSet<string> SupportedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        MembershipCodes.InstitutionalStatus.PastActive,
        MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal,
        MembershipCodes.InstitutionalStatus.ForcedWithdrawal,
        MembershipCodes.InstitutionalStatus.Reinstated,
        MembershipCodes.InstitutionalStatus.Deceased
    };

    public static IEndpointRouteBuilder MapInstitutionalStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/members/{memberId:guid}/institutional-status", RecordStatusAsync)
            .WithTags("Membership")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> RecordStatusAsync(
        Guid memberId,
        InstitutionalStatusTransitionRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageInstitutionalStatuses(httpContext.User))
        {
            return Results.Forbid();
        }

        var eventType = request.EventType.Trim().ToLowerInvariant();
        if (!SupportedTransitions.Contains(eventType))
        {
            return Results.BadRequest(new
            {
                message = "La transición institucional indicada no está habilitada por este flujo."
            });
        }

        var memberExists = await db.Members
            .AsNoTracking()
            .AnyAsync(x => x.Id == memberId, cancellationToken);
        if (!memberExists)
        {
            return Results.NotFound(new { message = "El Hermano indicado no existe." });
        }

        var targetWorkshopExists = await db.Organizations
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.OrganizationId && x.Type == "workshop", cancellationToken);
        if (!targetWorkshopExists)
        {
            return Results.NotFound(new { message = "El Taller indicado no existe." });
        }

        var latestStatus = await db.InstitutionalStatusEvents
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.EventType, x.EffectiveDate, x.OrganizationId })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestStatus is not null && request.EffectiveDate < latestStatus.EffectiveDate)
        {
            return Results.Conflict(new
            {
                message = "La fecha efectiva no puede ser anterior al último estado institucional registrado."
            });
        }

        if (latestStatus is not null &&
            string.Equals(latestStatus.EventType, eventType, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { message = "El Hermano ya registra ese estado institucional como último estado." });
        }

        var activeMemberships = await db.Memberships
            .Where(x => x.MemberId == memberId &&
                        x.Status == MembershipCodes.MembershipStatus.Active &&
                        x.EndDate == null)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        Membership? closedMembership = null;
        Membership? createdMembership = null;
        Guid? sourceOrganizationId = latestStatus?.OrganizationId;
        Guid targetOrganizationId = request.OrganizationId;

        if (eventType == MembershipCodes.InstitutionalStatus.PastActive)
        {
            var activeMembership = activeMemberships.FirstOrDefault(x => x.OrganizationId == request.OrganizationId);
            if (activeMembership is null)
            {
                return Results.Conflict(new
                {
                    message = "Past Activo requiere una pertenencia vigente al cuadro del Taller indicado."
                });
            }

            sourceOrganizationId = activeMembership.OrganizationId;
        }
        else if (eventType is MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal or
                 MembershipCodes.InstitutionalStatus.ForcedWithdrawal or
                 MembershipCodes.InstitutionalStatus.Deceased)
        {
            var activeMembership = activeMemberships.FirstOrDefault(x => x.OrganizationId == request.OrganizationId);
            if (activeMembership is null)
            {
                return Results.Conflict(new
                {
                    message = "La transición requiere una pertenencia vigente al Taller indicado."
                });
            }

            if (request.EffectiveDate <= activeMembership.StartDate)
            {
                return Results.BadRequest(new
                {
                    message = "La fecha efectiva debe ser posterior al inicio del segmento vigente de pertenencia."
                });
            }

            activeMembership.EndDate = request.EffectiveDate.AddDays(-1);
            activeMembership.Status = MembershipCodes.MembershipStatus.Closed;
            activeMembership.EndReason = eventType;
            activeMembership.EvidenceReference = request.EvidenceReference ?? activeMembership.EvidenceReference;
            closedMembership = activeMembership;
            sourceOrganizationId = activeMembership.OrganizationId;
        }
        else if (eventType == MembershipCodes.InstitutionalStatus.Reinstated)
        {
            if (latestStatus is null ||
                latestStatus.EventType != MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal)
            {
                return Results.Conflict(new
                {
                    message = "El reintegro de este flujo requiere un Retiro voluntario / En sueño previo como último estado."
                });
            }

            if (activeMemberships.Count > 0)
            {
                return Results.Conflict(new
                {
                    message = "No se puede reintegrar mientras exista una pertenencia activa."
                });
            }

            var previousMembership = await db.Memberships
                .AsNoTracking()
                .Where(x => x.MemberId == memberId)
                .OrderByDescending(x => x.EndDate ?? x.StartDate)
                .ThenByDescending(x => x.StartDate)
                .ThenByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousMembership is null)
            {
                return Results.Conflict(new
                {
                    message = "No existe un segmento histórico de pertenencia que permita sustentar el reintegro."
                });
            }

            sourceOrganizationId = previousMembership.OrganizationId;
            createdMembership = new Membership
            {
                MemberId = memberId,
                OrganizationId = request.OrganizationId,
                MembershipType = previousMembership.MembershipType,
                StartDate = request.EffectiveDate,
                Status = MembershipCodes.MembershipStatus.Active,
                EvidenceReference = request.EvidenceReference
            };
            db.Memberships.Add(createdMembership);
        }

        var statusEvent = new InstitutionalStatusEvent
        {
            MemberId = memberId,
            EventType = eventType,
            EffectiveDate = request.EffectiveDate,
            Reason = NormalizeOptional(request.Reason),
            OrganizationId = request.OrganizationId,
            EvidenceReference = NormalizeOptional(request.EvidenceReference),
            Notes = NormalizeOptional(request.Notes)
        };
        db.InstitutionalStatusEvents.Add(statusEvent);

        audit.Add(
            httpContext,
            "membership.institutional_status.recorded",
            nameof(InstitutionalStatusEvent),
            statusEvent.Id.ToString(),
            request.OrganizationId,
            AuditResults.Success,
            new
            {
                PreviousStatus = latestStatus?.EventType,
                NewStatus = eventType,
                request.EffectiveDate,
                SourceOrganizationId = sourceOrganizationId,
                TargetOrganizationId = targetOrganizationId,
                ClosedMembershipSegment = closedMembership?.Id,
                CreatedMembershipSegment = createdMembership?.Id
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/members/{memberId}/history",
            new InstitutionalStatusTransitionResponse(
                statusEvent.Id,
                memberId,
                latestStatus?.EventType,
                statusEvent.EventType,
                statusEvent.EffectiveDate,
                sourceOrganizationId,
                targetOrganizationId,
                closedMembership?.Id,
                createdMembership?.Id));
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record InstitutionalStatusTransitionRequest(
    string EventType,
    DateOnly EffectiveDate,
    Guid OrganizationId,
    string? Reason,
    string? EvidenceReference,
    string? Notes);

public sealed record InstitutionalStatusTransitionResponse(
    Guid EventId,
    Guid MemberId,
    string? PreviousStatus,
    string NewStatus,
    DateOnly EffectiveDate,
    Guid? SourceOrganizationId,
    Guid TargetOrganizationId,
    Guid? ClosedMembershipId,
    Guid? CreatedMembershipId);
