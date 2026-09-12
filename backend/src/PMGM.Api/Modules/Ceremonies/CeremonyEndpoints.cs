using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Notifications;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Modules.Ceremonies;

public static class CeremonyEndpoints
{
    private const int DefaultInitiationPublicationDays = 20;

    public static IEndpointRouteBuilder MapCeremonyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ceremonias")
            .WithTags("Ceremonias")
            .RequireAuthorization();

        group.MapPost("/solicitudes", CreateRequestAsync);
        group.MapPost("/solicitudes/{requestId:guid}/validaciones/regimen-interior", SetInternalAffairsValidationAsync);
        group.MapPost("/solicitudes/{requestId:guid}/revision-publicacion-insinuado", ReviewCandidatePublicationAsync);
        group.MapPost("/solicitudes/{requestId:guid}/aprobar-publicacion-insinuado", PublishCandidateAsync);
        group.MapPost("/solicitudes/{requestId:guid}/publicacion-insinuado", PublishCandidateAsync);
        group.MapGet("/solicitudes/{requestId:guid}/elegibilidad", GetEligibilityAsync);
        group.MapPost("/solicitudes/{requestId:guid}/autorizar", AuthorizeAsync);
        group.MapPost("/solicitudes/{requestId:guid}/registrar-iniciacion", RegisterInitiationAsync);
        group.MapGet("/portal-insinuados", GetCandidatePortalAsync);
        group.MapPost("/reglas/publicacion-iniciacion", SetInitiationPublicationRuleAsync);
        group.MapGet("/reglas/publicacion-iniciacion", GetInitiationPublicationRuleAsync);

        return endpoints;
    }

    private static async Task<IResult> RegisterInitiationAsync(
        Guid requestId,
        RegisterInitiationRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        GrandSecretariatDbContext secretariatDb,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests.Include(x => x.CandidatePerson)
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null) return Results.NotFound();
        if (!access.CanManageOrganization(httpContext.User, ceremony.OrganizationId)) return Results.Forbid();
        if (ceremony.CeremonyType != CeremonyCodes.Type.Initiation || ceremony.CandidatePersonId is null)
            return Results.BadRequest(new { message = "La operación sólo corresponde a una ceremonia de Iniciación." });
        if (ceremony.Status == CeremonyCodes.RequestStatus.Completed)
            return Results.Conflict(new { message = "La Iniciación ya fue registrada y el Aprendiz ya está activado." });
        if (ceremony.Status != CeremonyCodes.RequestStatus.Authorized)
            return Results.Conflict(new { message = "La ceremonia debe estar autorizada antes de registrar su realización." });
        if (request.CeremonyDate > ChileToday() || string.IsNullOrWhiteSpace(request.MinuteReference))
            return Results.BadRequest(new { message = "La fecha no puede ser futura y la referencia del acta es obligatoria." });

        var authorization = await secretariatDb.SecretariatDocuments.AsNoTracking()
            .Where(x => x.RelatedCeremonyRequestId == requestId &&
                        x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorization &&
                        x.Status == GrandSecretariatCodes.DocumentStatus.Issued)
            .OrderByDescending(x => x.IssuedAtUtc)
            .Select(x => new { x.DocumentCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (authorization is null)
            return Results.Conflict(new { message = "No puede registrarse la Iniciación sin una Plancha de autorización vigente emitida por Gran Secretaría." });

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await db.Members.AnyAsync(x => x.PersonId == ceremony.CandidatePersonId.Value, cancellationToken))
            return Results.Conflict(new { message = "La persona ya se encuentra activada como miembro." });

        var evidence = $"{authorization.DocumentCode}; {request.MinuteReference.Trim()}";
        var member = new Member { PersonId = ceremony.CandidatePersonId.Value };
        db.Members.Add(member);
        db.Memberships.Add(new PMGM.Api.Modules.Membership.Entities.Membership { Member = member, OrganizationId = ceremony.OrganizationId, MembershipType = "regular", StartDate = request.CeremonyDate, Status = MembershipCodes.MembershipStatus.Active, EvidenceReference = evidence });
        db.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent { Member = member, OrganizationId = ceremony.OrganizationId, EventType = MembershipCodes.InstitutionalStatus.Active, EffectiveDate = request.CeremonyDate, EvidenceReference = evidence, Reason = "Activación por ceremonia de Iniciación realizada." });
        db.DegreeEvents.Add(new DegreeEvent { Member = member, OrganizationId = ceremony.OrganizationId, Degree = "apprentice", EventType = MembershipCodes.DegreeEvent.Initiation, EffectiveDate = request.CeremonyDate, EvidenceReference = evidence });
        ceremony.Member = member;
        ceremony.Status = CeremonyCodes.RequestStatus.Completed;

        audit.Add(httpContext, "ceremony.initiation.completed", nameof(CeremonyRequest), ceremony.Id.ToString(), ceremony.OrganizationId, AuditResults.Success,
            new { member.Id, ceremony.CandidatePersonId, request.CeremonyDate, request.MinuteReference, authorization.DocumentCode, degree = "apprentice", membershipStatus = "active" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(new { ceremony.Id, ceremony.Status, memberId = member.Id, membershipStatus = "active", degree = "apprentice", effectiveDate = request.CeremonyDate, authorization.DocumentCode });
    }

    private static async Task<IResult> CreateRequestAsync(
        CreateCeremonyRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!IsCeremonyTypeValid(request.CeremonyType))
        {
            return Results.BadRequest(new { message = "El tipo de ceremonia indicado no es válido." });
        }

        if (!access.CanManageOrganization(httpContext.User, request.OrganizationId) &&
            !access.CanEvaluateCeremonies(httpContext.User))
        {
            return Results.Forbid();
        }

        var organizationExists = await db.Organizations.AnyAsync(x => x.Id == request.OrganizationId, cancellationToken);
        if (!organizationExists)
        {
            return Results.NotFound(new { message = "El Taller u organización no existe." });
        }

        if (request.CeremonyType == CeremonyCodes.Type.Initiation)
        {
            if (request.CandidatePersonId is null || request.MemberId is not null)
            {
                return Results.BadRequest(new { message = "Una iniciación requiere una persona insinuada y no debe vincularse todavía a un Miembro." });
            }

            var candidateExists = await db.People.AnyAsync(x => x.Id == request.CandidatePersonId.Value, cancellationToken);
            if (!candidateExists)
            {
                return Results.NotFound(new { message = "La persona insinuada no existe en la base maestra." });
            }
        }
        else
        {
            if (request.MemberId is null || request.CandidatePersonId is not null)
            {
                return Results.BadRequest(new { message = "Aumento de salario y exaltación requieren un hermano registrado." });
            }

            var memberBelongsToWorkshop = await db.Memberships.AnyAsync(
                x => x.MemberId == request.MemberId.Value &&
                     x.OrganizationId == request.OrganizationId &&
                     x.EndDate == null,
                cancellationToken);

            if (!memberBelongsToWorkshop)
            {
                return Results.BadRequest(new { message = "El hermano no registra una pertenencia vigente al Taller solicitante." });
            }
        }

        var entity = new CeremonyRequest
        {
            OrganizationId = request.OrganizationId,
            CeremonyType = request.CeremonyType,
            MemberId = request.MemberId,
            CandidatePersonId = request.CandidatePersonId,
            ProposedDate = request.ProposedDate,
            Status = CeremonyCodes.RequestStatus.UnderReview,
            Notes = request.Notes
        };

        db.CeremonyRequests.Add(entity);
        audit.Add(
            httpContext,
            "ceremony.request.created",
            nameof(CeremonyRequest),
            entity.Id.ToString(),
            entity.OrganizationId,
            AuditResults.Success,
            new
            {
                entity.CeremonyType,
                entity.ProposedDate,
                entity.Status
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/ceremonias/solicitudes/{entity.Id}", new
        {
            entity.Id,
            entity.OrganizationId,
            entity.CeremonyType,
            entity.MemberId,
            entity.CandidatePersonId,
            entity.ProposedDate,
            entity.Status
        });
    }

    private static async Task<IResult> SetInternalAffairsValidationAsync(
        Guid requestId,
        CeremonyValidationRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var canValidate = access.HasOrderScope(httpContext.User) &&
                          access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.RegimenInterior);

        if (!canValidate)
        {
            return Results.Forbid();
        }

        if (!IsDecisionStatusValid(request.Status))
        {
            return Results.BadRequest(new { message = "El estado de validación indicado no es válido." });
        }

        var ceremony = await db.CeremonyRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null)
        {
            return Results.NotFound();
        }

        var validation = new CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = CeremonyCodes.ValidationType.InternalAffairs,
            Status = request.Status,
            AsOfDate = ChileToday(),
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        db.CeremonyValidations.Add(validation);
        audit.Add(
            httpContext,
            "ceremony.internal_affairs_validation.recorded",
            nameof(CeremonyValidation),
            validation.Id.ToString(),
            ceremony.OrganizationId,
            AuditResults.Success,
            new
            {
                validation.ValidationType,
                validation.Status,
                validation.AsOfDate
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            validation.Id,
            validation.CeremonyRequestId,
            validation.ValidationType,
            validation.Status,
            validation.AsOfDate,
            validation.SourceReference
        });
    }

    private static async Task<IResult> ReviewCandidatePublicationAsync(
        Guid requestId,
        CandidatePublicationReviewRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.HasOrderScope(httpContext.User) ||
            !access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranSecretaria))
        {
            return Results.Forbid();
        }

        if (request.Decision is not CeremonyCodes.ValidationStatus.Observed and not CeremonyCodes.ValidationStatus.Rejected)
        {
            return Results.BadRequest(new { message = "La revisión de publicación sólo admite observar o rechazar. Para aprobar utilice la acción de aprobación de Gran Secretaría." });
        }

        var ceremony = await db.CeremonyRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null)
        {
            return Results.NotFound();
        }

        if (ceremony.CeremonyType != CeremonyCodes.Type.Initiation || ceremony.CandidatePersonId is null)
        {
            return Results.BadRequest(new { message = "La revisión de publicación sólo aplica a fichas de insinuados para iniciación." });
        }

        var alreadyPublished = await db.CandidatePublications.AnyAsync(
            x => x.CeremonyRequestId == requestId && x.Status == CeremonyCodes.PublicationStatus.Published,
            cancellationToken);
        if (alreadyPublished)
        {
            return Results.Conflict(new { message = "La ficha ya fue aprobada y publicada; no puede observarse o rechazarse por este flujo." });
        }

        var review = new CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = CeremonyCodes.ValidationType.CandidatePublicationReview,
            Status = request.Decision,
            AsOfDate = ChileToday(),
            SourceReference = request.SourceReference,
            Notes = request.Notes
        };

        ceremony.Status = request.Decision == CeremonyCodes.ValidationStatus.Observed
            ? CeremonyCodes.RequestStatus.Observed
            : CeremonyCodes.RequestStatus.Rejected;

        db.CeremonyValidations.Add(review);
        audit.Add(
            httpContext,
            "ceremony.candidate_publication.reviewed",
            nameof(CeremonyRequest),
            ceremony.Id.ToString(),
            ceremony.OrganizationId,
            AuditResults.Success,
            new
            {
                decision = review.Status,
                ceremony.Status,
                review.AsOfDate
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            ceremony.Id,
            ceremony.Status,
            reviewId = review.Id,
            decision = review.Status,
            review.AsOfDate
        });
    }

    private static async Task<IResult> PublishCandidateAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        IInstitutionalNotificationService notifications,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests
            .Include(x => x.CandidatePerson)
            .Include(x => x.Organization)
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (ceremony is null)
        {
            return Results.NotFound();
        }

        if (ceremony.CeremonyType != CeremonyCodes.Type.Initiation || ceremony.CandidatePersonId is null || ceremony.CandidatePerson is null)
        {
            return Results.BadRequest(new { message = "La publicación de insinuado sólo aplica a solicitudes de iniciación." });
        }

        if (!access.CanManageCandidatePublications(httpContext.User, ceremony.OrganizationId))
        {
            return Results.Forbid();
        }

        var candidateName = string.Join(' ', new[] { ceremony.CandidatePerson.FirstNames, ceremony.CandidatePerson.LastNames }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var workshopName = ceremony.Organization.Name;

        var existingPublication = await db.CandidatePublications
            .AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId && x.Status == CeremonyCodes.PublicationStatus.Published)
            .OrderByDescending(x => x.PublishedFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingPublication is not null)
        {
            var reconciled = await QueueCandidatePublicationNotificationsAsync(
                existingPublication,
                candidateName,
                workshopName,
                httpContext,
                db,
                notifications,
                cancellationToken);

            return Results.Ok(new
            {
                existingPublication.Id,
                existingPublication.CeremonyRequestId,
                existingPublication.PersonId,
                existingPublication.OrganizationId,
                existingPublication.PublishedFromUtc,
                existingPublication.RequiredDays,
                existingPublication.RuleCode,
                existingPublication.Status,
                alreadyPublished = true,
                notificationRecipients = reconciled.Recipients,
                notificationsCreated = reconciled.Created
            });
        }

        var today = ChileToday();
        var requiredDays = await GetMinimumPublicationDaysAsync(db, today, cancellationToken);

        var publication = new CandidatePublication
        {
            CeremonyRequestId = requestId,
            PersonId = ceremony.CandidatePersonId.Value,
            OrganizationId = ceremony.OrganizationId,
            PublishedFromUtc = DateTimeOffset.UtcNow,
            RequiredDays = requiredDays,
            RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
            Status = CeremonyCodes.PublicationStatus.Published
        };

        var review = new CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = CeremonyCodes.ValidationType.CandidatePublicationReview,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = today,
            SourceReference = publication.Id.ToString(),
            Notes = "Ficha aprobada por Gran Secretaría para publicación institucional."
        };

        ceremony.Status = CeremonyCodes.RequestStatus.UnderReview;
        db.CandidatePublications.Add(publication);
        db.CeremonyValidations.Add(review);
        audit.Add(
            httpContext,
            "ceremony.candidate_publication.approved_by_grand_secretariat",
            nameof(CandidatePublication),
            publication.Id.ToString(),
            publication.OrganizationId,
            AuditResults.Success,
            new
            {
                publication.PublishedFromUtc,
                publication.RequiredDays,
                publication.RuleCode,
                publication.Status,
                reviewId = review.Id
            });

        await db.SaveChangesAsync(cancellationToken);

        var dispatch = await QueueCandidatePublicationNotificationsAsync(
            publication,
            candidateName,
            workshopName,
            httpContext,
            db,
            notifications,
            cancellationToken);

        audit.Add(
            httpContext,
            "ceremony.candidate_publication.notifications_queued",
            nameof(CandidatePublication),
            publication.Id.ToString(),
            publication.OrganizationId,
            AuditResults.Success,
            new
            {
                dispatch.Recipients,
                dispatch.Created,
                channel = NotificationCodes.Channel.Internal
            });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/ceremonias/solicitudes/{requestId}/aprobar-publicacion-insinuado", new
        {
            publication.Id,
            publication.CeremonyRequestId,
            publication.PersonId,
            publication.OrganizationId,
            publication.PublishedFromUtc,
            publication.RequiredDays,
            publication.RuleCode,
            publication.Status,
            alreadyPublished = false,
            notificationRecipients = dispatch.Recipients,
            notificationsCreated = dispatch.Created
        });
    }

    private static async Task<NotificationDispatchResult> QueueCandidatePublicationNotificationsAsync(
        CandidatePublication publication,
        string candidateName,
        string workshopName,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalNotificationService notifications,
        CancellationToken cancellationToken)
    {
        var subjects = await GetActiveMemberNotificationSubjectsAsync(db, cancellationToken);
        var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? httpContext.TraceIdentifier;
        var created = 0;

        foreach (var subject in subjects)
        {
            var result = await notifications.QueueAsync(new QueueNotificationCommand(
                NotificationCodes.Template.CandidatePublicationApproved,
                1,
                NotificationCodes.Type.CandidatePublicationApproved,
                subject,
                null,
                new[] { NotificationCodes.Channel.Internal },
                new Dictionary<string, string?>
                {
                    ["candidateName"] = candidateName,
                    ["workshopName"] = workshopName
                },
                $"candidate-publication:{publication.Id}:subject:{subject}",
                correlationId,
                publication.Id.ToString(),
                "/candidates",
                false,
                null), cancellationToken);

            if (result.Created) created++;
        }

        return new NotificationDispatchResult(subjects.Count, created);
    }

    private static async Task<IReadOnlyList<string>> GetActiveMemberNotificationSubjectsAsync(
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT mil."Subject"
                FROM core.member_identity_links AS mil
                WHERE mil."RevokedAtUtc" IS NULL
                  AND EXISTS (
                      SELECT 1
                      FROM core.memberships AS ms
                      WHERE ms."MemberId" = mil."MemberId"
                        AND ms."EndDate" IS NULL
                        AND lower(ms."Status") = 'active'
                  )
                ORDER BY mil."Subject";
                """;

            var subjects = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0)) subjects.Add(reader.GetString(0));
            }

            return subjects;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static async Task<IResult> GetEligibilityAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var context = await BuildEligibilityContextAsync(requestId, db, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (!access.CanEvaluateCeremonies(httpContext.User) &&
            !access.CanReadOrganization(httpContext.User, context.Request.OrganizationId))
        {
            return Results.Forbid();
        }

        return Results.Ok(ToEligibilityResponse(context));
    }

    private static async Task<IResult> AuthorizeAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var canAuthorize = access.HasOrderScope(httpContext.User) &&
                           access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GranSecretaria);

        if (!canAuthorize)
        {
            return Results.Forbid();
        }

        var ceremony = await db.CeremonyRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (ceremony is null)
        {
            return Results.NotFound();
        }

        if (ceremony.Status == CeremonyCodes.RequestStatus.Authorized)
        {
            return Results.Conflict(new { message = "La ceremonia ya se encuentra autorizada." });
        }

        var context = await BuildEligibilityContextAsync(requestId, db, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (!context.Decision.CanAuthorize)
        {
            ceremony.Status = context.Decision.Status == "observed"
                ? CeremonyCodes.RequestStatus.Observed
                : CeremonyCodes.RequestStatus.Rejected;

            audit.Add(
                httpContext,
                "ceremony.authorization.rejected",
                nameof(CeremonyRequest),
                ceremony.Id.ToString(),
                ceremony.OrganizationId,
                AuditResults.Rejected,
                new
                {
                    ceremony.CeremonyType,
                    ceremony.Status,
                    evaluatedAsOf = context.AsOfDate,
                    blockingRequirements = context.Decision.Requirements
                        .Where(x => x.Status != CeremonyCodes.ValidationStatus.Approved)
                        .Select(x => x.Code)
                        .ToArray()
                });

            await db.SaveChangesAsync(cancellationToken);
            return Results.Conflict(new
            {
                message = "La ceremonia no cumple todos los requisitos obligatorios para ser autorizada.",
                eligibility = ToEligibilityResponse(context)
            });
        }

        var today = ChileToday();
        AddFrozenValidation(db, requestId, CeremonyCodes.ValidationType.Treasury, context.Treasury!.Id, today, "Regularidad del Taller validada por Gran Tesorería.");
        AddFrozenValidation(db, requestId, CeremonyCodes.ValidationType.Hospitalaria, context.Hospitalaria!.Id, today, "Reposiciones y obligaciones validadas por Gran Hospitalaria.");

        if (context.Publication is not null)
        {
            AddFrozenValidation(db, requestId, CeremonyCodes.ValidationType.CandidatePublication, context.Publication.Id, today,
                $"Publicación del insinuado: {context.Publication.CompletedDays} días válidos de {context.Publication.RequiredDays} requeridos.");
        }

        ceremony.Status = CeremonyCodes.RequestStatus.Authorized;
        var authorizedAtUtc = DateTimeOffset.UtcNow;

        audit.Add(
            httpContext,
            "ceremony.authorization.approved",
            nameof(CeremonyRequest),
            ceremony.Id.ToString(),
            ceremony.OrganizationId,
            AuditResults.Success,
            new
            {
                ceremony.CeremonyType,
                ceremony.Status,
                authorizedAtUtc,
                evaluatedAsOf = context.AsOfDate,
                publicationRequiredDays = context.Publication?.RequiredDays,
                publicationCompletedDays = context.Publication?.CompletedDays
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            ceremony.Id,
            ceremony.Status,
            authorizedAtUtc,
            eligibility = ToEligibilityResponse(context)
        });
    }

    private static async Task<IResult> GetCandidatePortalAsync(
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db.CandidatePublications
            .AsNoTracking()
            .Where(x =>
                x.Status == CeremonyCodes.PublicationStatus.Published &&
                x.PublishedFromUtc <= now &&
                (x.PublishedUntilUtc == null || x.PublishedUntilUtc >= now))
            .OrderBy(x => x.PublishedFromUtc)
            .Select(x => new
            {
                x.OrganizationId,
                x.Person.FirstNames,
                x.Person.LastNames,
                WorkshopName = x.Organization.Name,
                WorkshopNumber = x.Organization.Number,
                x.PublishedFromUtc,
                x.PublishedUntilUtc,
                x.RequiredDays,
                x.RuleCode,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var visible = rows
            .Select(x => new CandidatePublicationPublicDto(
                DisplayName: $"{x.FirstNames} {x.LastNames}".Trim(),
                WorkshopName: x.WorkshopName,
                WorkshopNumber: x.WorkshopNumber,
                PublishedFromUtc: x.PublishedFromUtc,
                PublishedUntilUtc: x.PublishedUntilUtc,
                RequiredDays: x.RequiredDays,
                ElapsedDays: Math.Max(0, (int)Math.Floor((now - x.PublishedFromUtc).TotalDays)),
                ComplianceDateUtc: x.PublishedFromUtc.AddDays(x.RequiredDays),
                RuleCode: x.RuleCode,
                Status: x.Status))
            .ToList();

        return Results.Ok(new
        {
            culture = "es-CL",
            portal = "Insinuados en período de publicación",
            total = visible.Count,
            items = visible
        });
    }

    private static async Task<IResult> SetInitiationPublicationRuleAsync(
        InitiationPublicationRuleRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        var canManageRule = access.HasOrderScope(httpContext.User) &&
                            access.HasRole(httpContext.User, InstitutionalRoles.GranLogiaAdmin);

        if (!canManageRule)
        {
            return Results.Forbid();
        }

        if (request.MinimumDays is < 1 or > 365)
        {
            return Results.BadRequest(new { message = "El plazo mínimo debe estar entre 1 y 365 días." });
        }

        var duplicate = await db.InstitutionalRuleSettings.AnyAsync(
            x => x.Code == CeremonyCodes.Rules.InitiationPublicationMinimumDays &&
                 x.EffectiveFrom == request.EffectiveFrom,
            cancellationToken);

        if (duplicate)
        {
            return Results.Conflict(new { message = "Ya existe una configuración con la misma fecha de vigencia." });
        }

        var previousOpenRule = await db.InstitutionalRuleSettings
            .Where(x => x.Code == CeremonyCodes.Rules.InitiationPublicationMinimumDays &&
                        x.Status == "active" &&
                        x.EffectiveFrom < request.EffectiveFrom &&
                        x.EffectiveTo == null)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousOpenRule is not null)
        {
            previousOpenRule.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        }

        var rule = new InstitutionalRuleSetting
        {
            Code = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
            Value = request.MinimumDays.ToString(CultureInfo.InvariantCulture),
            EffectiveFrom = request.EffectiveFrom,
            Status = "active",
            SourceReference = request.SourceReference
        };

        db.InstitutionalRuleSettings.Add(rule);
        audit.Add(
            httpContext,
            "ceremony.rule.initiation_publication.versioned",
            nameof(InstitutionalRuleSetting),
            rule.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                rule.Code,
                minimumDays = request.MinimumDays,
                rule.EffectiveFrom,
                rule.Status
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Created("/api/ceremonias/reglas/publicacion-iniciacion", new
        {
            rule.Id,
            rule.Code,
            minimumDays = request.MinimumDays,
            rule.EffectiveFrom,
            rule.EffectiveTo,
            rule.Status,
            rule.SourceReference
        });
    }

    private static async Task<IResult> GetInitiationPublicationRuleAsync(
        DateOnly? asOf,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.HasOrderScope(httpContext.User) &&
            !access.HasRole(httpContext.User, InstitutionalRoles.TallerAdmin, InstitutionalRoles.TallerSecretaria))
        {
            return Results.Forbid();
        }

        var cutoff = asOf ?? ChileToday();
        var days = await GetMinimumPublicationDaysAsync(db, cutoff, cancellationToken);

        return Results.Ok(new
        {
            code = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
            minimumDays = days,
            asOf = cutoff,
            defaultAppliedWhenNoRuleExists = DefaultInitiationPublicationDays
        });
    }

    private static async Task<EligibilityContext?> BuildEligibilityContextAsync(
        Guid requestId,
        PmgmDbContext db,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (ceremony is null)
        {
            return null;
        }

        var today = ChileToday();
        var now = DateTimeOffset.UtcNow;

        var internalAffairs = await db.CeremonyValidations
            .AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.ValidationType == CeremonyCodes.ValidationType.InternalAffairs)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var grandMaster = await db.CeremonyValidations
            .AsNoTracking()
            .Where(x => x.CeremonyRequestId == requestId &&
                        x.ValidationType == CeremonyCodes.ValidationType.GrandMaster)
            .OrderByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var treasury = await db.FinancialRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == ceremony.OrganizationId &&
                        x.MemberId == null &&
                        x.Scope == TreasuryCodes.RegularityScope.Organization &&
                        x.AsOfDate <= today)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var hospitalaria = await db.HospitalariaRegularitySnapshots
            .AsNoTracking()
            .Where(x => x.OrganizationId == ceremony.OrganizationId && x.AsOfDate <= today)
            .OrderByDescending(x => x.AsOfDate)
            .ThenByDescending(x => x.RecordedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        CandidatePublicationSnapshot? publicationSnapshot = null;
        if (ceremony.CeremonyType == CeremonyCodes.Type.Initiation)
        {
            var publication = await db.CandidatePublications
                .AsNoTracking()
                .Where(x => x.CeremonyRequestId == requestId)
                .OrderByDescending(x => x.PublishedFromUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (publication is not null)
            {
                var validUntil = publication.PublishedUntilUtc is null || publication.PublishedUntilUtc > now
                    ? now
                    : publication.PublishedUntilUtc.Value;

                var completedDays = validUntil < publication.PublishedFromUtc
                    ? 0
                    : Math.Max(0, (int)Math.Floor((validUntil - publication.PublishedFromUtc).TotalDays));

                publicationSnapshot = new CandidatePublicationSnapshot(
                    publication.Id,
                    publication.Status,
                    publication.RequiredDays,
                    completedDays,
                    publication.RuleCode,
                    publication.PublishedFromUtc,
                    publication.PublishedUntilUtc);
            }
        }

        var evidence = publicationSnapshot is null
            ? null
            : new CandidatePublicationEvidence(
                publicationSnapshot.Id,
                publicationSnapshot.Status,
                publicationSnapshot.RequiredDays,
                publicationSnapshot.CompletedDays,
                publicationSnapshot.RuleCode);

        var decision = CeremonyEligibilityPolicy.Evaluate(
            ceremony.CeremonyType,
            internalAffairs?.Status,
            treasury?.Status,
            hospitalaria?.Status,
            grandMaster?.Status,
            evidence);

        return new EligibilityContext(ceremony, internalAffairs, treasury, hospitalaria, grandMaster, publicationSnapshot, decision, today);
    }

    private static object ToEligibilityResponse(EligibilityContext context) => new
    {
        requestId = context.Request.Id,
        context.Request.CeremonyType,
        context.Request.OrganizationId,
        evaluatedAsOf = context.AsOfDate,
        status = context.Decision.Status,
        canAuthorize = context.Decision.CanAuthorize,
        requirements = context.Decision.Requirements,
        evidence = new
        {
            regimenInteriorValidationId = context.InternalAffairs?.Id,
            treasurySnapshotId = context.Treasury?.Id,
            hospitalariaSnapshotId = context.Hospitalaria?.Id,
            granMaestriaValidationId = context.GrandMaster?.Id,
            publication = context.Publication
        }
    };

    private static void AddFrozenValidation(
        PmgmDbContext db,
        Guid requestId,
        string type,
        Guid sourceId,
        DateOnly asOfDate,
        string notes)
    {
        db.CeremonyValidations.Add(new CeremonyValidation
        {
            CeremonyRequestId = requestId,
            ValidationType = type,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = asOfDate,
            SourceReference = sourceId.ToString(),
            Notes = notes
        });
    }

    private static async Task<int> GetMinimumPublicationDaysAsync(
        PmgmDbContext db,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var rule = await db.InstitutionalRuleSettings
            .AsNoTracking()
            .Where(x => x.Code == CeremonyCodes.Rules.InitiationPublicationMinimumDays &&
                        x.Status == "active" &&
                        x.EffectiveFrom <= asOf &&
                        (x.EffectiveTo == null || x.EffectiveTo >= asOf))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        return rule is not null &&
               int.TryParse(rule.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var configuredDays) &&
               configuredDays > 0
            ? configuredDays
            : DefaultInitiationPublicationDays;
    }

    private static bool IsCeremonyTypeValid(string value)
        => value is CeremonyCodes.Type.Initiation or CeremonyCodes.Type.WageIncrease or CeremonyCodes.Type.Exaltation;

    private static bool IsDecisionStatusValid(string value)
        => value is CeremonyCodes.ValidationStatus.Approved
            or CeremonyCodes.ValidationStatus.Observed
            or CeremonyCodes.ValidationStatus.Rejected
            or CeremonyCodes.ValidationStatus.ExceptionApproved;

    private static DateOnly ChileToday()
    {
        var chileNow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago");
        return DateOnly.FromDateTime(chileNow.DateTime);
    }

    private sealed record EligibilityContext(
        CeremonyRequest Request,
        CeremonyValidation? InternalAffairs,
        FinancialRegularitySnapshot? Treasury,
        HospitalariaRegularitySnapshot? Hospitalaria,
        CeremonyValidation? GrandMaster,
        CandidatePublicationSnapshot? Publication,
        CeremonyEligibilityDecision Decision,
        DateOnly AsOfDate);

    private sealed record CandidatePublicationSnapshot(
        Guid Id,
        string Status,
        int RequiredDays,
        int CompletedDays,
        string RuleCode,
        DateTimeOffset PublishedFromUtc,
        DateTimeOffset? PublishedUntilUtc);

    private sealed record NotificationDispatchResult(int Recipients, int Created);
}

public sealed record CreateCeremonyRequest(
    Guid OrganizationId,
    string CeremonyType,
    Guid? MemberId,
    Guid? CandidatePersonId,
    DateOnly? ProposedDate,
    string? Notes);

public sealed record CeremonyValidationRequest(
    string Status,
    string? SourceReference,
    string? Notes);

public sealed record CandidatePublicationReviewRequest(
    string Decision,
    string? SourceReference,
    string? Notes);

public sealed record RegisterInitiationRequest(DateOnly CeremonyDate, string MinuteReference);

public sealed record InitiationPublicationRuleRequest(
    int MinimumDays,
    DateOnly EffectiveFrom,
    string? SourceReference);
