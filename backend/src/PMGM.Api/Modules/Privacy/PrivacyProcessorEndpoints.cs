using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Privacy.Entities;

namespace PMGM.Api.Modules.Privacy;

public static class PrivacyProcessorEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyProcessorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/privacy")
            .WithTags("Privacidad y Ley 21.719")
            .RequireAuthorization();

        group.MapGet("/processors", GetProcessorsAsync);
        group.MapPost("/processors", CreateProcessorAsync);
        group.MapGet("/international-transfers", GetInternationalTransfersAsync);
        group.MapPost("/international-transfers", CreateInternationalTransferAsync);

        return endpoints;
    }

    private static async Task<IResult> GetProcessorsAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var rows = await db.DataProcessors
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Service)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Service,
                x.Purpose,
                x.AgreementReference,
                x.TransferMechanism,
                x.Status,
                x.EffectiveFrom,
                x.EffectiveTo
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateProcessorAsync(
        CreateDataProcessorRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Service) ||
            string.IsNullOrWhiteSpace(request.Purpose))
        {
            return Results.BadRequest(new { message = "Proveedor, servicio y finalidad son obligatorios." });
        }

        var entity = new DataProcessor
        {
            Name = request.Name.Trim(),
            Service = request.Service.Trim(),
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

        db.DataProcessors.Add(entity);
        audit.Add(httpContext, "privacy.processor.created", nameof(DataProcessor), entity.Id.ToString(), null, AuditResults.Success,
            new { entity.Name, entity.Service, entity.Status });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/processors/{entity.Id}", new { entity.Id });
    }

    private static async Task<IResult> GetInternationalTransfersAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManagePrivacy(httpContext.User))
        {
            return Results.Forbid();
        }

        var rows = await db.InternationalDataTransfers
            .AsNoTracking()
            .OrderBy(x => x.DestinationCountry)
            .ThenBy(x => x.Recipient)
            .Select(x => new
            {
                x.Id,
                x.DataProcessingActivityId,
                processingActivityCode = x.DataProcessingActivity.Code,
                x.DataProcessorId,
                processorName = x.DataProcessor == null ? null : x.DataProcessor.Name,
                x.DestinationCountry,
                x.Recipient,
                x.LegalMechanism,
                x.Safeguards,
                x.Status,
                x.EffectiveFrom,
                x.EffectiveTo
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> CreateInternationalTransferAsync(
        CreateInternationalTransferRequest request,
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

        var activityExists = await db.DataProcessingActivities
            .AnyAsync(x => x.Id == request.DataProcessingActivityId, cancellationToken);

        if (!activityExists)
        {
            return Results.BadRequest(new { message = "La actividad de tratamiento indicada no existe." });
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

        var entity = new InternationalDataTransfer
        {
            DataProcessingActivityId = request.DataProcessingActivityId,
            DataProcessorId = request.DataProcessorId,
            DestinationCountry = request.DestinationCountry.Trim(),
            Recipient = request.Recipient.Trim(),
            LegalMechanism = request.LegalMechanism.Trim(),
            Safeguards = request.Safeguards,
            Status = request.Status,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo
        };

        db.InternationalDataTransfers.Add(entity);
        audit.Add(httpContext, "privacy.international_transfer.created", nameof(InternationalDataTransfer), entity.Id.ToString(), null, AuditResults.Success,
            new { entity.DataProcessingActivityId, entity.DataProcessorId, entity.DestinationCountry, entity.Recipient, entity.Status });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/privacy/international-transfers/{entity.Id}", new { entity.Id });
    }
}

public sealed record CreateDataProcessorRequest(
    string Name,
    string Service,
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

public sealed record CreateInternationalTransferRequest(
    Guid DataProcessingActivityId,
    Guid? DataProcessorId,
    string DestinationCountry,
    string Recipient,
    string LegalMechanism,
    string? Safeguards,
    string Status,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
