using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.CandidateIntake;

public static class CandidateWorkshopIntakeEndpoints
{
    public static IEndpointRouteBuilder MapCandidateWorkshopIntakeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/insinuados/taller/organizaciones", GetWorkshopOrganizationsAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();
        endpoints.MapGet("/api/insinuados/taller/organizaciones/{organizationId:guid}/patrocinadores", GetEligibleSponsorsAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();
        endpoints.MapPost("/api/insinuados/taller/solicitudes", CreateWorkshopRequestAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();
        endpoints.MapPut("/api/insinuados/taller/solicitudes/{requestId:guid}/patrocinador", AssignWorkshopSponsorAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();
        endpoints.MapGet("/api/insinuados/taller/solicitudes", GetWorkshopQueueAsync)
            .WithTags("Ficha privada de insinuados")
            .RequireAuthorization();
        endpoints.MapGet("/api/insinuados/regimen-interior/alertas-rechazo", GetOrderRejectionAlertsAsync)
            .WithTags("Régimen Interior")
            .RequireAuthorization();

        endpoints.MapCandidateRePresentationReviewEndpoints();
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

    /// <summary>
    /// Catálogo privado de presentadores/patrocinadores: solo miembros activos
    /// del Taller receptor. No expone RUT, email ni hermanos de otros Talleres.
    /// La selección NO sustituye la validación al guardar el patrocinio.
    /// </summary>
    private static async Task<IResult> GetEligibleSponsorsAsync(
        Guid organizationId, string? q, HttpContext httpContext,
        PmgmDbContext coreDb, IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageLodgeSecretariat(httpContext.User, organizationId))
            return Results.Forbid();

        var search = q?.Trim();
        if (search is { Length: > 80 } || search?.Any(char.IsControl) == true)
            return Results.BadRequest(new {
                code = "CANDIDATE_SPONSOR_SEARCH_INVALID",
                message = "La búsqueda admite un máximo de 80 caracteres."
            });
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var memberships = await coreDb.Memberships.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId &&
                m.Status == MembershipCodes.MembershipStatus.Active &&
                (m.StartDate == null || m.StartDate <= today) &&
                (m.EndDate == null || m.EndDate >= today))
            .Select(m => new {
                m.MemberId, m.Member.CurrentDegree, m.Member.Person.FirstNames, m.Member.Person.LastNames
            }).Distinct().ToListAsync(cancellationToken);
        var ids = memberships.Select(m => m.MemberId).Distinct().ToArray();
        var recognized = new[] {
            MembershipCodes.InstitutionalStatus.Active,
            MembershipCodes.InstitutionalStatus.Reinstated,
            MembershipCodes.InstitutionalStatus.Inactive,
            MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal,
            MembershipCodes.InstitutionalStatus.ForcedWithdrawal,
            MembershipCodes.InstitutionalStatus.Deceased
        };
        var statusEvents = await coreDb.InstitutionalStatusEvents.AsNoTracking()
            .Where(e => ids.Contains(e.MemberId) && e.EffectiveDate <= today &&
                recognized.Contains(e.EventType))
            .OrderByDescending(e => e.EffectiveDate)
            .ThenByDescending(e => e.RecordedAtUtc)
            .Select(e => new { e.MemberId, e.EventType })
            .ToListAsync(cancellationToken);
        var latest = statusEvents.GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g => g.First().EventType);
        var choices = memberships
            .Where(m => InstitutionalDegree.TryParse(m.CurrentDegree, out var degree) && degree >= 3)
            .Where(m => !latest.TryGetValue(m.MemberId, out var status) ||
                status is MembershipCodes.InstitutionalStatus.Active or
                    MembershipCodes.InstitutionalStatus.Reinstated)
            .Select(m => new {
                id = m.MemberId,
                name = (m.FirstNames + " " + m.LastNames).Trim()
            })
            .Where(m => string.IsNullOrWhiteSpace(search) ||
                m.name.Contains(search!, StringComparison.OrdinalIgnoreCase))
            .GroupBy(m => m.id).Select(g => g.First())
            .OrderBy(m => m.name, StringComparer.CurrentCultureIgnoreCase)
            .Take(40).ToArray();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { items = choices });
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
        var normalizedId = rut is null ? null : new string(rut.Where(char.IsLetterOrDigit).ToArray());
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

        var presentationDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);

        // La interfaz antigua todavía no envía origen: NULL conserva compatibilidad
        // sin inventar fuentes. Si llega un origen, la validación es estricta.
        CandidateOriginValidationResult? origin = null;
        if (request.OriginKind is not null ||
            request.RecommenderMemberId is not null ||
            request.OriginOtherDescription is not null)
        {
            var accredited = request.OriginKind == CandidateOriginPolicy.Brother &&
                request.RecommenderMemberId is Guid recommenderId
                ? await CandidateOriginAccreditation.VerifyAsync(
                    coreDb, recommenderId, presentationDate, cancellationToken)
                : null;

            origin = CandidateOriginPolicy.ValidateNew(
                request.OriginKind, request.RecommenderMemberId,
                request.OriginOtherDescription, accredited, presentationDate);
            if (!origin.IsValid)
                return Results.BadRequest(new { code = origin.ErrorCode,
                    message = "El origen del candidato no está acreditado o contiene datos incompatibles." });
        }
        // El recomendante identifica procedencia y puede venir de otro Taller.
        // El patrocinador FORMAL sí debe pertenecer al Taller que recibe el expediente.
        CandidateOriginVerifiedMember? sponsor = null;
        if (request.SponsorMemberId is Guid sponsorId)
        {
            sponsor = await CandidateOriginAccreditation.VerifyWorkshopSponsorAsync(
                coreDb, sponsorId, organization.Id, presentationDate, cancellationToken);
            if (sponsor is null || !sponsor.IsActive)
                return Results.BadRequest(new {
                    code = "CANDIDATE_SPONSOR_NOT_ACTIVE_IN_WORKSHOP",
                    message = "El patrocinador formal debe ser Maestro/a activo/a del Taller receptor."
                });
        }
        var person = await coreDb.People.SingleOrDefaultAsync(x => x.Rut != null &&
            x.Rut.Replace(".", "").Replace("-", "").Replace(" ", "").ToUpper() == normalizedId, cancellationToken);
        if (person is not null)
        {
            var activeRequest = await coreDb.CeremonyRequests.AnyAsync(x =>
                x.CandidatePersonId == person.Id &&
                x.CeremonyType == CeremonyCodes.Type.Initiation &&
                x.Status != CeremonyCodes.RequestStatus.Completed &&
                x.Status != CeremonyCodes.RequestStatus.Rejected, cancellationToken);
            if (activeRequest)
                return Results.Conflict(new { message = "Ya existe un expediente de iniciación activo para esta identificación. Continúe el expediente existente." });

        }
        var priorRejection = person is null ? null :
            await CandidateRePresentationGate.FindLatestAsync(coreDb, person.Id, cancellationToken);
        var rePresentation = CandidateRePresentationGate.Evaluate(
            priorRejection, presentationDate, request.RejectionCausesRemedied, request.RePresentationSourceReference);
        if (!rePresentation.CanProceed)
            return Results.Conflict(new { code = rePresentation.Code, message = rePresentation.Reason });
        if (person is null)
        {
            person = new Person { FirstNames = firstNames, LastNames = lastNames, Rut = rut };
            coreDb.People.Add(person);
        }

        var entity = new CeremonyRequest
        {
            OrganizationId = organization.Id,
            CeremonyType = CeremonyCodes.Type.Initiation,
            RequiresFormalInterviewAssignments = true,
            CandidatePersonId = person.Id,
            CandidateOriginKind = origin?.Kind,
            CandidateRecommenderMemberId = origin?.RecommenderMemberId,
            CandidateSponsorMemberId = sponsor?.MemberId,
            CandidateOriginOtherDescription = origin?.OtherDescription,
            Status = CeremonyCodes.RequestStatus.Draft,
            Notes = "Expediente de insinuación iniciado por Secretaría Logial."
        };
        coreDb.CeremonyRequests.Add(entity);
        var rePresentationValidation = CandidateRePresentationGate.AddEvidence(
            coreDb, entity.Id, presentationDate, rePresentation);
        if (rePresentationValidation is not null)
            audit.Add(httpContext, "candidate.representation.evidence_recorded", nameof(CeremonyValidation),
                rePresentationValidation.Id.ToString(), organization.Id, AuditResults.Success,
                new { priorValidationId = rePresentation.PriorRejection!.ValidationId,
                    priorRequestId = rePresentation.PriorRejection.CeremonyRequestId,
                    rePresentation.PriorRejection.RejectionDate,
                    newCeremonyRequestId = entity.Id, referenceRecorded = true });
        audit.Add(httpContext, "candidate.intake.created", nameof(CeremonyRequest), entity.Id.ToString(),
            organization.Id, AuditResults.Success, new { entity.CandidatePersonId, entity.CeremonyType, entity.Status,
                originKind = entity.CandidateOriginKind, recommenderRegistered = entity.CandidateRecommenderMemberId.HasValue,
                workshopSponsorAccredited = entity.CandidateSponsorMemberId.HasValue });
        await coreDb.SaveChangesAsync(cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Created($"/api/ceremonias/solicitudes/{entity.Id}", new CandidateWorkshopQueueItemDto(
            entity.Id, person.FirstNames, person.LastNames, person.Rut, $"{person.FirstNames} {person.LastNames}".Trim(),
            organization.Name, organization.Number, null, entity.Status, false, false,
            CandidateIntakeCodes.ReviewStatus.Pending, entity.CreatedAtUtc, null));
    }

    // Un expediente puede nacer desde Internet o la recomendación de otro
    // Taller, pero no se presenta institucionalmente sin patrocinador local.
    private static async Task<IResult> AssignWorkshopSponsorAsync(
        Guid requestId, AssignCandidateWorkshopSponsor request,
        HttpContext httpContext, PmgmDbContext coreDb,
        IInstitutionalAccessService access, IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (request.SponsorMemberId == Guid.Empty)
            return Results.BadRequest(new {
                code = "CANDIDATE_SPONSOR_REQUIRED",
                message = "Seleccione un miembro activo del Taller receptor."
            });
        var ceremony = await coreDb.CeremonyRequests
            .SingleOrDefaultAsync(x => x.Id == requestId &&
                x.CeremonyType == CeremonyCodes.Type.Initiation, cancellationToken);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanManageLodgeSecretariat(httpContext.User, ceremony.OrganizationId))
            return Results.Forbid();
        if (ceremony.Status is CeremonyCodes.RequestStatus.Authorized or
            CeremonyCodes.RequestStatus.Completed or CeremonyCodes.RequestStatus.Rejected)
            return Results.Conflict(new { code = "CANDIDATE_SPONSOR_CASE_CLOSED" });
        if (await coreDb.CandidatePublications.AsNoTracking()
            .AnyAsync(x => x.CeremonyRequestId == requestId &&
                (x.Status == CeremonyCodes.PublicationStatus.Published ||
                 x.Status == CeremonyCodes.PublicationStatus.Completed), cancellationToken))
            return Results.Conflict(new { code = "CANDIDATE_SPONSOR_ALREADY_PUBLISHED",
                message = "La presentación publicada no puede modificarse desde el borrador." });

        if (ceremony.CandidateSponsorMemberId == request.SponsorMemberId)
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            return Results.Ok(new { ceremonyRequestId = requestId,
                sponsorMemberId = request.SponsorMemberId, unchanged = true });
        }
        if (ceremony.CandidateSponsorMemberId.HasValue)
            return Results.Conflict(new {
                code = "CANDIDATE_SPONSOR_CHANGE_REQUIRES_FORMAL_CORRECTION",
                message = "Un patrocinador ya registrado sólo puede cambiar mediante corrección formal auditada."
            });
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var verified = await CandidateOriginAccreditation.VerifyWorkshopSponsorAsync(
            coreDb, request.SponsorMemberId, ceremony.OrganizationId, today, cancellationToken);
        if (verified is null || !verified.IsActive)
            return Results.BadRequest(new {
                code = "CANDIDATE_SPONSOR_NOT_ACTIVE_IN_WORKSHOP",
                message = "El patrocinador formal debe ser Maestro/a activo/a del Taller receptor."
            });

        ceremony.CandidateSponsorMemberId = request.SponsorMemberId;
        // La auditoría institucional conserva autor, fecha, id de expediente y
        // motivo registrado, pero no replica nombres ni IDs personales.
        audit.Add(httpContext, "candidate.sponsor.assigned",
            nameof(CeremonyRequest), requestId.ToString("D"), ceremony.OrganizationId,
            AuditResults.Success, new { workshopSponsorAccredited = true, source = "core.memberships" });
        await coreDb.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new { ceremonyRequestId = requestId,
            sponsorMemberId = request.SponsorMemberId, unchanged = false });
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
                RutOrInstitutionalId = x.CandidatePerson != null ? x.CandidatePerson.Rut : null,
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
                candidate.RutOrInstitutionalId,
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

public sealed record CreateCandidateWorkshopRequest(
    Guid OrganizationId, string FirstNames, string LastNames, string RutOrInstitutionalId,
    bool RejectionCausesRemedied = false, string? RePresentationSourceReference = null,
    string? OriginKind = null, Guid? RecommenderMemberId = null,
    string? OriginOtherDescription = null, Guid? SponsorMemberId = null);

public sealed record AssignCandidateWorkshopSponsor(
    Guid SponsorMemberId, string? Reason = null);

public sealed record CandidateWorkshopQueueItemDto(
    Guid CeremonyRequestId,
    string FirstNames,
    string LastNames,
    string? RutOrInstitutionalId,
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
