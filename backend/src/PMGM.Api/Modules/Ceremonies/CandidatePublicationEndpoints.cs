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
                OrganizationId = x.Organization.Id,
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

        var result = rows
            .Where(x => access.CanReadOrganization(httpContext.User, x.OrganizationId))
            .Select(x => new CandidatePublicationPublicDto(
                DisplayName: string.Join(' ', new[] { x.FirstNames, x.LastNames }.Where(value => !string.IsNullOrWhiteSpace(value))),
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

        return Results.Ok(result);
    }
}
