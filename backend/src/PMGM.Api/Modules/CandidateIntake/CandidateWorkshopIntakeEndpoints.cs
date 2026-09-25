using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Ceremonies;

namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidateWorkshopIntakeEndpoints
{
    public static IEndpointRouteBuilder MapCandidateWorkshopIntakeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/insinuados/taller/organizaciones", GetWorkshopOrganizationsAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();
        endpoints.MapPost("/api/insinuados/taller/solicitudes", CreateWorkshopRequestAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();
        endpoints.MapGet("/api/insinuados/taller/solicitudes", GetWorkshopQueueAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();
        endpoints.MapGet("/api/insinuados/regimen-interior/alertas-rechazo", GetOrderRejectionAlertsAsync)
            .WithTags("Régimen Interior")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetWorkshopOrganizationsAsync(
        HttpContext httpContext,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var organizationIds = httpContext.User.Claims
            .Where(x => x.Type == InstitutionalClaims.Organization)
            .Select(x => Guid.TryParse(x.Value, out var id) ? id : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .Where(id => access.CanManageLodgeSecretariat(httpContext.User, id))
            .ToArray();
        if (organizationIds.Length == 0 && access.HasOrderScope(httpContext.User) &&
            access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin))
        {
            organizationIds = await coreDb.Organizations.AsNoTracking()
                .Where(x => x.Type == "lodge" || x.Type == "workshop")
                .Select(x => x.Id)
                .ToArrayAsync(cancellationToken);
        }

        var items = await coreDb.Organizations.AsNoTracking()
            .Where(x => organizationIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new CandidateWorkshopOrganizationDto(x.Id, x.Name, x.Number))
            .ToListAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static async Task<IResult> CreateWorkshopRequestAsync(
        CreateCandidateWorkshopRequest request,
        HttpContext httpContext,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var firstNames = request.FirstNames?.Trim();
        var lastNames = request.LastNames?.Trim();
        var rut = request.RutOrInstitutionalId?.Trim().ToUpperInvariant();
        if (request.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(firstNames) ||
            string.IsNullOrWhiteSpace(lastNames) || string.IsNullOrWhiteSpace(rut) ||
            firstNames.Length > 160 || lastNames.Length > 160 || rut.Length > 16)
            return Results.BadRequest(new { message = "Ingrese Taller, nombres, apellidos e identificación válida (máximo 16 caracteres)." });
        if (!access.CanManageLodgeSecretariat(httpContext.User, request.OrganizationId))
            return Results.Forbid();

        var organization = await coreDb.Organizations.AsNoTracking()
            .Where(x => x.Id == request.OrganizationId)
            .Select(x => new { x.Id, x.Name, x.Number })
            .SingleOrDefaultAsync(cancellationToken);
        if (organization is null) return Results.NotFound(new { message = "El Taller seleccionado no existe." });

        var person = await coreDb.People.SingleOrDefaultAsync(x => x.Rut == rut, cancellationToken);
        if (person is not null)
        {
            var activeRequest = await coreDb.CeremonyRequests.AnyAsync(x =>
                x.CandidatePersonId == person.Id &&
                x.CeremonyType == CeremonyCodes.Type.Initiation &&
                x.Status != CeremonyCodes.RequestStatus.Completed &&
                x.Status != CeremonyCodes.RequestStatus.Rejected, cancellationToken);
            if (activeRequest)
                return Results.Conflict(new { message = "Ya existe un expediente de iniciación activo para esta identificación. Continúe el expediente existente." });

            var recentRejection = await coreDb.CeremonyValidations.AnyAsync(x =>
                x.CeremonyRequest.CandidatePersonId == person.Id &&
                (x.ValidationType == CeremonyCodes.ValidationType.CandidateThirdDegreeReview ||
                 x.ValidationType == CeremonyCodes.ValidationType.CandidateFinalBallot) &&
                x.Status == CeremonyCodes.ValidationStatus.Rejected &&
                x.AsOfDate > DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-1)), cancellationToken);
            if (recentRejection)
                return Results.Conflict(new { message = "Existe un rechazo previo en el último año. El protocolo requiere esperar un año y subsanar sus causas antes de una nueva insinuación." });
        }
        else
        {
            person = new Person { FirstNames = firstNames, LastNames = lastNames, Rut = rut };
            coreDb.People.Add(person);
        }

        var entity = new CeremonyRequest
        {
            OrganizationId = organization.Id,
            CeremonyType = CeremonyCodes.Type.Initiation,
            CandidatePersonId = person.Id,
            Status = CeremonyCodes.RequestStatus.Draft,
            Notes = "Expediente de insinuación iniciado por Secretaría Logial."
        };
        coreDb.CeremonyRequests.Add(entity);
        audit.Add(httpContext, "candidate.intake.created", nameof(CeremonyRequest), entity.Id.ToString(),
            organization.Id, AuditResults.Success, new { entity.CandidatePersonId, entity.CeremonyType, entity.Status });
        await coreDb.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Created($"/api/ceremonias/solicitudes/{entity.Id}", new CandidateWorkshopQueueItemDto(
            entity.Id, person.FirstNames, person.LastNames, $"{person.FirstNames} {person.LastNames}".Trim(),
            organization.Name, organization.Number, null, entity.Status, false, false,
            CandidateIntakeCodes.ReviewStatus.Pending, entity.CreatedAtUtc, null));
    }

    private static async Task<IResult> GetOrderRejectionAlertsAsync(
        HttpContext httpContext,
        PmgmDbContext coreDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.HasOrderScope(httpContext.User) ||
            !access.HasRole(httpContext.User, InstitutionalRoles.RegimenInterior, InstitutionalRoles.GranLogiaAdmin))
            return Results.Forbid();
        var alertRows = await coreDb.CeremonyValidations.AsNoTracking()
            .Where(x => (x.ValidationType == CeremonyCodes.ValidationType.CandidateThirdDegreeReview ||
                         x.ValidationType == CeremonyCodes.ValidationType.CandidateFinalBallot) &&
                        x.Status == CeremonyCodes.ValidationStatus.Rejected)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new
            {
                PersonId = x.CeremonyRequest.CandidatePersonId!.Value,
                FirstNames = x.CeremonyRequest.CandidatePerson!.FirstNames,
                LastNames = x.CeremonyRequest.CandidatePerson!.LastNames,
                WorkshopName = x.CeremonyRequest.Organization.Name,
                WorkshopNumber = x.CeremonyRequest.Organization.Number,
                x.AsOfDate,
                x.SourceReference,
                x.Notes,
                x.ValidationType,
                x.RecordedAtUtc
            })
            .Take(500)
            .ToListAsync(cancellationToken);
        var alerts = alertRows.Select(x => new CandidateOrderRejectionAlertDto(
                x.PersonId,
                x.FirstNames,
                x.LastNames,
                x.WorkshopName,
                x.WorkshopNumber,
                x.AsOfDate,
                x.SourceReference,
                x.Notes,
                CandidateRejectionPolicy.GetReason(x.ValidationType)))
            .ToList();
        /*
            The query above intentionally retrieves both blocking rejection sources.
            Reason text is assigned after materialization to keep the EF query simple
            and the normative mapping covered by unit tests.
        */
        /* legacy projection removed:
            .Select(x => new CandidateOrderRejectionAlertDto(
        */
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = alerts.Count, items = alerts });
    }

    private static async Task<IResult> GetWorkshopQueueAsync(
        HttpContext httpContext,
        PmgmDbContext coreDb,
        CandidateIntakeDbContext intakeDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeOperations(httpContext.User) && !access.CanManageGrandSecretariat(httpContext.User))
            return Results.Forbid();

        var candidates = await coreDb.CeremonyRequests
            .AsNoTracking()
            .Where(x => x.CeremonyType == CeremonyCodes.Type.Initiation && x.CandidatePersonId != null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .Select(x => new
            {
                x.Id,
                x.OrganizationId,
                x.CandidatePersonId,
                WorkshopName = x.Organization.Name,
                WorkshopNumber = x.Organization.Number,
                FirstNames = x.CandidatePerson != null ? x.CandidatePerson.FirstNames : "",
                LastNames = x.CandidatePerson != null ? x.CandidatePerson.LastNames : "",
                x.ProposedDate,
                x.Status,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var allowed = candidates
            .Where(x => access.CanManageOrganization(httpContext.User, x.OrganizationId) || access.CanManageGrandSecretariat(httpContext.User))
            .ToList();

        var requestIds = allowed.Select(x => x.Id).ToArray();
        if (requestIds.Length == 0)
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { total = 0, items = Array.Empty<CandidateWorkshopQueueItemDto>() });
        }

        var profiles = await intakeDb.CandidateIntakeProfiles
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId))
            .Select(x => new { x.CeremonyRequestId, x.PhotoVersionId })
            .ToDictionaryAsync(x => x.CeremonyRequestId, cancellationToken);

        var reviewRows = await coreDb.CeremonyValidations
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.CeremonyRequestId) && x.ValidationType == CeremonyCodes.ValidationType.CandidatePublicationReview)
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new { x.CeremonyRequestId, x.Status, x.RecordedAtUtc })
            .ToListAsync(cancellationToken);
        var latestReviews = reviewRows
            .GroupBy(x => x.CeremonyRequestId)
            .ToDictionary(group => group.Key, group => group.First().Status);

        var published = (await coreDb.CandidatePublications
                .AsNoTracking()
                .Where(x => requestIds.Contains(x.CeremonyRequestId) && x.Status == CeremonyCodes.PublicationStatus.Published)
                .Select(x => x.CeremonyRequestId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var personIds = allowed.Select(x => x.CandidatePersonId!.Value).Distinct().ToArray();
        var blockedRows = await coreDb.CeremonyValidations.AsNoTracking()
            .Where(x => (x.ValidationType == CeremonyCodes.ValidationType.CandidateThirdDegreeReview ||
                         x.ValidationType == CeremonyCodes.ValidationType.CandidateFinalBallot) &&
                        x.Status == CeremonyCodes.ValidationStatus.Rejected &&
                        personIds.Contains(x.CeremonyRequest.CandidatePersonId!.Value))
            .OrderByDescending(x => x.RecordedAtUtc)
            .Select(x => new
            {
                PersonId = x.CeremonyRequest.CandidatePersonId!.Value,
                x.CeremonyRequestId,
                WorkshopName = x.CeremonyRequest.Organization.Name,
                x.AsOfDate,
                x.ValidationType
            })
            .ToListAsync(cancellationToken);
        var blockedByPerson = blockedRows.GroupBy(x => x.PersonId).ToDictionary(x => x.Key, x => x.First());

        var items = allowed.Select(candidate =>
        {
            profiles.TryGetValue(candidate.Id, out var profile);
            var reviewStatus = published.Contains(candidate.Id)
                ? CandidateIntakeCodes.ReviewStatus.Approved
                : latestReviews.TryGetValue(candidate.Id, out var review)
                    ? MapReviewStatus(review)
                    : CandidateIntakeCodes.ReviewStatus.Pending;

            blockedByPerson.TryGetValue(candidate.CandidatePersonId!.Value, out var blocked);
            return new CandidateWorkshopQueueItemDto(
                candidate.Id,
                candidate.FirstNames,
                candidate.LastNames,
                $"{candidate.FirstNames} {candidate.LastNames}".Trim(),
                candidate.WorkshopName,
                candidate.WorkshopNumber,
                candidate.ProposedDate,
                candidate.Status,
                profile is not null,
                profile?.PhotoVersionId is not null,
                reviewStatus,
                candidate.CreatedAtUtc,
                blocked is null ? null : new CandidateOrderBlockAlertDto(blocked.CeremonyRequestId, blocked.WorkshopName, blocked.AsOfDate, CandidateRejectionPolicy.GetReason(blocked.ValidationType)));
        }).ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { total = items.Count, items });
    }

    private static string MapReviewStatus(string? status)
        => status switch
        {
            CeremonyCodes.ValidationStatus.Approved => CandidateIntakeCodes.ReviewStatus.Approved,
            CeremonyCodes.ValidationStatus.Observed => CandidateIntakeCodes.ReviewStatus.Observed,
            CeremonyCodes.ValidationStatus.Rejected => CandidateIntakeCodes.ReviewStatus.Rejected,
            _ => CandidateIntakeCodes.ReviewStatus.Pending
        };
}

public sealed record CandidateWorkshopOrganizationDto(Guid Id, string Name, string? Number);

public sealed record CreateCandidateWorkshopRequest(Guid OrganizationId, string FirstNames, string LastNames, string RutOrInstitutionalId);

public sealed record CandidateWorkshopQueueItemDto(
    Guid CeremonyRequestId,
    string FirstNames,
    string LastNames,
    string DisplayName,
    string WorkshopName,
    string? WorkshopNumber,
    DateOnly? ProposedDate,
    string RequestStatus,
    bool ProfileAvailable,
    bool PhotoAvailable,
    string ReviewStatus,
    DateTimeOffset CreatedAtUtc,
    CandidateOrderBlockAlertDto? OrderLevelAlert);

public sealed record CandidateOrderBlockAlertDto(
    Guid PreviousCeremonyRequestId,
    string PreviousWorkshopName,
    DateOnly RejectionDate,
    string Reason);

public sealed record CandidateOrderRejectionAlertDto(
    Guid PersonId,
    string FirstNames,
    string LastNames,
    string WorkshopName,
    string? WorkshopNumber,
    DateOnly RejectionDate,
    string? SourceReference,
    string? Notes,
    string Reason);
