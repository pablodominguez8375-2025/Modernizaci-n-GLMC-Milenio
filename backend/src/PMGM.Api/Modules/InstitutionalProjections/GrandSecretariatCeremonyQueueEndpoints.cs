using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.GrandSecretariat;

namespace PMGM.Api.Modules.InstitutionalProjections;

public static class GrandSecretariatCeremonyQueueEndpoints
{
    public static IEndpointRouteBuilder MapGrandSecretariatCeremonyQueueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/institutional/gran-secretaria/ceremonias-autorizadas", GetAuthorizedCeremoniesAsync)
            .WithTags("Proyecciones institucionales")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetAuthorizedCeremoniesAsync(
        HttpContext httpContext,
        PmgmDbContext institutionalDb,
        GrandSecretariatDbContext secretariatDb,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (!access.CanManageGrandSecretariat(httpContext.User)) return Results.Forbid();

        var ceremonies = await institutionalDb.CeremonyRequests
            .AsNoTracking()
            .Where(x => x.Status == CeremonyCodes.RequestStatus.Authorized)
            .OrderBy(x => x.ProposedDate)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.OrganizationId,
                OrganizationName = x.Organization.Name,
                OrganizationNumber = x.Organization.Number,
                x.CeremonyType,
                x.ProposedDate,
                x.Status,
                x.CreatedAtUtc
            })
            .Take(250)
            .ToListAsync(cancellationToken);

        var requestIds = ceremonies.Select(x => x.Id).ToList();
        var issuedIds = requestIds.Count == 0
            ? new HashSet<Guid>()
            : (await secretariatDb.SecretariatDocuments
                .AsNoTracking()
                .Where(x => x.RelatedCeremonyRequestId != null &&
                            requestIds.Contains(x.RelatedCeremonyRequestId.Value) &&
                            x.DocumentType == GrandSecretariatCodes.DocumentType.CeremonyAuthorization &&
                            x.Status == GrandSecretariatCodes.DocumentStatus.Issued)
                .Select(x => x.RelatedCeremonyRequestId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var items = ceremonies.Select(x => new GrandSecretariatCeremonyQueueItemDto(
            x.Id,
            x.OrganizationId,
            x.OrganizationName,
            x.OrganizationNumber,
            x.CeremonyType,
            x.ProposedDate,
            x.Status,
            issuedIds.Contains(x.Id),
            x.CreatedAtUtc)).ToList();

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new GrandSecretariatCeremonyQueueResponse(items.Count, items));
    }
}

public sealed record GrandSecretariatCeremonyQueueItemDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string? OrganizationNumber,
    string CeremonyType,
    DateOnly? ProposedDate,
    string Status,
    bool FormalAuthorizationIssued,
    DateTimeOffset CreatedAtUtc);

public sealed record GrandSecretariatCeremonyQueueResponse(
    int Total,
    IReadOnlyList<GrandSecretariatCeremonyQueueItemDto> Items);
