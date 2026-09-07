using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Ceremonies;

public static class CandidatePublicationEndpoints
{
    public static IEndpointRouteBuilder MapCandidatePublicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/candidate-publications")
            .WithTags("Portal de Insinuados")
            .RequireAuthorization();

        group.MapGet("/active", GetActivePublicationsAsync);
        return endpoints;
    }

    private static async Task<IResult> GetActivePublicationsAsync(
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
                x.Id,
                x.CeremonyRequestId,
                candidate = new
                {
                    x.Person.Id,
                    x.Person.FirstNames,
                    x.Person.LastNames
                },
                workshop = new
                {
                    x.Organization.Id,
                    x.Organization.Name,
                    x.Organization.Number
                },
                x.PublishedFromUtc,
                x.PublishedUntilUtc,
                x.RequiredDays,
                x.RuleCode,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var result = rows
            .Where(x => access.CanReadOrganization(httpContext.User, x.workshop.Id))
            .Select(x => new
            {
                x.Id,
                x.CeremonyRequestId,
                x.candidate,
                x.workshop,
                x.PublishedFromUtc,
                x.PublishedUntilUtc,
                x.RequiredDays,
                elapsedDays = Math.Max(0, (int)Math.Floor((now - x.PublishedFromUtc).TotalDays)),
                complianceDateUtc = x.PublishedFromUtc.AddDays(x.RequiredDays),
                x.RuleCode,
                x.Status
            });

        return Results.Ok(result);
    }
}
