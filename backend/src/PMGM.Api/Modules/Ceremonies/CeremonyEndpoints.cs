using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Hospitalaria.Entities;
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
        group.MapPost("/solicitudes/{requestId:guid}/publicacion-insinuado", PublishCandidateAsync);
        group.MapGet("/solicitudes/{requestId:guid}/elegibilidad", GetEligibilityAsync);
        group.MapPost("/solicitudes/{requestId:guid}/autorizar", AuthorizeAsync);
        group.MapGet("/portal-insinuados", GetCandidatePortalAsync);
        group.MapPost("/reglas/publicacion-iniciacion", SetInitiationPublicationRuleAsync);
        group.MapGet("/reglas/publicacion-iniciacion", GetInitiationPublicationRuleAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateRequestAsync(
        CreateCeremonyRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
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

    private static async Task<IResult> PublishCandidateAsync(
        Guid requestId,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        var ceremony = await db.CeremonyRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (ceremony is null)
        {
            return Results.NotFound();
        }

        if (ceremony.CeremonyType != CeremonyCodes.Type.Initiation || ceremony.CandidatePersonId is null)
        {
            return Results.BadRequest(new { message = "La publicación de insinuado sólo aplica a solicitudes de iniciación." });
        }

        if (!access.CanManageCandidatePublications(httpContext.User, ceremony.OrganizationId))
        {
            return Results.Forbid();
        }

        var alreadyPublished = await db.CandidatePublications.AnyAsync(
            x => x.CeremonyRequestId == requestId &&
                 x.Status == CeremonyCodes.PublicationStatus.Published &&
                 x.PublishedUntilUtc == null,
            cancellationToken);

        if (alreadyPublished)
        {
            return Results.Conflict(new { message = "La solicitud ya tiene una publicación vigente del insinuado." });
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

        db.CandidatePublications.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/ceremonias/solicitudes/{requestId}/publicacion-insinuado", new
        {
            publication.Id,
            publication.CeremonyRequestId,
            publication.PersonId,
            publication.OrganizationId,
            publication.PublishedFromUtc,
            publication.RequiredDays,
            publication.RuleCode,
            publication.Status
        });
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
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            ceremony.Id,
            ceremony.Status,
            authorizedAtUtc = DateTimeOffset.UtcNow,
            eligibility = ToEligibilityResponse(context)
        });
    }

    private static async Task<IResult> GetCandidatePortalAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
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
            .Where(x => access.CanReadOrganization(httpContext.User, x.OrganizationId))
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
            evidence);

        return new EligibilityContext(ceremony, internalAffairs, treasury, hospitalaria, publicationSnapshot, decision, today);
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

public sealed record InitiationPublicationRuleRequest(
    int MinimumDays,
    DateOnly EffectiveFrom,
    string? SourceReference);
