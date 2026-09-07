using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyProcessorLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyProcessorLifecycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/privacy")
            .WithTags("Privacidad — Ciclo de vida de terceros")
            .RequireAuthorization();

        group.MapPost("/processors/{id:guid}/versions", CreateProcessorVersionAsync);
        group.MapPost("/processors/{id:guid}/retire", RetireProcessorAsync);
        group.MapPost("/international-transfers/{id:guid}/versions", CreateTransferVersionAsync);
        group.MapPost("/international-transfers/{id:guid}/retire", RetireTransferAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateProcessorVersionAsync(
        Guid id,
        CreateDataProcessorVersionRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var current = await db.DataProcessors.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (current is null)
        {
            return Results.NotFound(new { message = "El encargado/proveedor indicado no existe." });
        }

        if (request.EffectiveFrom <= current.EffectiveFrom)
        {
            return Results.BadRequest(new { message = "La nueva versión debe comenzar después de la versión vigente." });
        }

        if (string.IsNullOrWhiteSpace(request.Purpose))
        {
            return Results.BadRequest(new { message = "La finalidad del encargado/proveedor es obligatoria." });
        }

        current.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        current.Status = PrivacyCodes.Status.Retired;

        var next = new DataProcessor
        {
            Name = current.Name,
            Service = current.Service,
            Purpose = request.Purpose.Trim(),
            CountriesJson = request.CountriesJson,
            DataCategoriesJson = request.DataCategoriesJson,
            SubjectCategoriesJson = request.SubjectCategoriesJson,
            SubprocessorsJson = request.SubprocessorsJson,
            AgreementReference = request.AgreementReference,
            TransferMechanism = request.TransferMechanism,
            IncidentObligations = request.IncidentObligations,
            Status = request.Status,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo
        };

        db.DataProcessors.Add(next);
        audit.Add(
            httpContext,
            "privacy.processor.versioned",
            nameof(DataProcessor),
            next.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                previousId = current.Id,
                next.Name,
                next.Service,
                next.EffectiveFrom,
                next.Status
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/processors/{next.Id}", new
        {
            previousId = current.Id,
            newId = next.Id,
            current.EffectiveTo,
            current.Status,
            newStatus = next.Status
        });
    }

    private static async Task<IResult> RetireProcessorAsync(
        Guid id,
        RetirePrivacyRecordRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var entity = await db.DataProcessors.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "El encargado/proveedor indicado no existe." });
        }

        if (entity.Status == PrivacyCodes.Status.Retired)
        {
            return Results.Conflict(new { message = "El encargado/proveedor ya está dado de baja." });
        }

        var effectiveTo = request.EffectiveTo ?? TodayInChile();
        if (effectiveTo < entity.EffectiveFrom)
        {
            return Results.BadRequest(new { message = "La fecha de baja no puede ser anterior al inicio de vigencia." });
        }

        entity.EffectiveTo = effectiveTo;
        entity.Status = PrivacyCodes.Status.Retired;

        audit.Add(
            httpContext,
            "privacy.processor.retired",
            nameof(DataProcessor),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new { entity.Name, entity.Service, entity.EffectiveTo, request.EvidenceReference });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status, entity.EffectiveTo });
    }

    private static async Task<IResult> CreateTransferVersionAsync(
        Guid id,
        CreateInternationalTransferVersionRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var current = await db.InternationalDataTransfers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (current is null)
        {
            return Results.NotFound(new { message = "La transferencia internacional indicada no existe." });
        }

        if (request.EffectiveFrom <= current.EffectiveFrom)
        {
            return Results.BadRequest(new { message = "La nueva versión debe comenzar después de la versión vigente." });
        }

        if (request.DataProcessorId is not null &&
            !await db.DataProcessors.AnyAsync(x => x.Id == request.DataProcessorId.Value, cancellationToken))
        {
            return Results.BadRequest(new { message = "El encargado/proveedor indicado no existe." });
        }

        if (string.IsNullOrWhiteSpace(request.DestinationCountry) ||
            string.IsNullOrWhiteSpace(request.Recipient) ||
            string.IsNullOrWhiteSpace(request.LegalMechanism))
        {
            return Results.BadRequest(new { message = "País de destino, receptor y mecanismo jurídico son obligatorios." });
        }

        current.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        current.Status = PrivacyCodes.Status.Retired;

        var next = new InternationalDataTransfer
        {
            DataProcessingActivityId = current.DataProcessingActivityId,
            DataProcessorId = request.DataProcessorId,
            DestinationCountry = request.DestinationCountry.Trim(),
            Recipient = request.Recipient.Trim(),
            LegalMechanism = request.LegalMechanism.Trim(),
            Safeguards = request.Safeguards,
            Status = request.Status,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo
        };

        db.InternationalDataTransfers.Add(next);
        audit.Add(
            httpContext,
            "privacy.international_transfer.versioned",
            nameof(InternationalDataTransfer),
            next.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                previousId = current.Id,
                next.DataProcessingActivityId,
                next.DataProcessorId,
                next.DestinationCountry,
                next.Recipient,
                next.EffectiveFrom,
                next.Status
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/international-transfers/{next.Id}", new
        {
            previousId = current.Id,
            newId = next.Id,
            current.EffectiveTo,
            current.Status,
            newStatus = next.Status
        });
    }

    private static async Task<IResult> RetireTransferAsync(
        Guid id,
        RetirePrivacyRecordRequest request,
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        IAuditService audit,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var entity = await db.InternationalDataTransfers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "La transferencia internacional indicada no existe." });
        }

        if (entity.Status == PrivacyCodes.Status.Retired)
        {
            return Results.Conflict(new { message = "La transferencia internacional ya está dada de baja." });
        }

        var effectiveTo = request.EffectiveTo ?? TodayInChile();
        if (effectiveTo < entity.EffectiveFrom)
        {
            return Results.BadRequest(new { message = "La fecha de baja no puede ser anterior al inicio de vigencia." });
        }

        entity.EffectiveTo = effectiveTo;
        entity.Status = PrivacyCodes.Status.Retired;

        audit.Add(
            httpContext,
            "privacy.international_transfer.retired",
            nameof(InternationalDataTransfer),
            entity.Id.ToString(),
            null,
            AuditResults.Success,
            new
            {
                entity.DataProcessingActivityId,
                entity.DataProcessorId,
                entity.DestinationCountry,
                entity.Recipient,
                entity.EffectiveTo,
                request.EvidenceReference
            });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entity.Id, entity.Status, entity.EffectiveTo });
    }

    private static DateOnly TodayInChile()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        var chileNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        return DateOnly.FromDateTime(chileNow.DateTime);
    }
}

public sealed record CreateDataProcessorVersionRequest(
    string Purpose,
    string CountriesJson,
    string DataCategoriesJson,
    string? SubjectCategoriesJson,
    string? SubprocessorsJson,
    string? AgreementReference,
    string? TransferMechanism,
    string? IncidentObligations,
    string Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record CreateInternationalTransferVersionRequest(
    Guid? DataProcessorId,
    string DestinationCountry,
    string Recipient,
    string LegalMechanism,
    string? Safeguards,
    string Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record RetirePrivacyRecordRequest(
    DateOnly? EffectiveTo,
    string? EvidenceReference);
