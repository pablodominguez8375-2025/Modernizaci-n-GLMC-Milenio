using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;

namespace PMGM.Api.Modules.Treasury;

public static class MemberSelfTreasuryEndpoints
{
    public static IEndpointRouteBuilder MapMemberSelfTreasuryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/member-self/treasury", GetSelfStatementAsync)
            .WithTags("Member Self Service")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetSelfStatementAsync(
        HttpContext httpContext,
        PmgmDbContext db,
        TreasuryLedgerDbContext ledger,
        IInstitutionalMemberContextResolver resolver,
        CancellationToken cancellationToken)
    {
        var context = await resolver.ResolveAsync(httpContext.User, cancellationToken);
        if (context is null)
            return Results.NotFound(new { message = "La identidad autenticada no está vinculada a un Hermano institucional activo." });

        var currentMembership = await db.Memberships
            .AsNoTracking()
            .Where(x => x.MemberId == context.MemberId &&
                        x.Status == MembershipCodes.MembershipStatus.Active &&
                        x.EndDate == null)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.OrganizationId,
                Organization = x.Organization.Name,
                OrganizationNumber = x.Organization.Number
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (currentMembership is null)
            return Results.NotFound(new { message = "El Hermano no registra un Taller vigente para consultar su estado de cuenta." });

        var statement = await MemberTreasuryStatementProjection.BuildAsync(
            ledger,
            currentMembership.OrganizationId,
            context.MemberId,
            cancellationToken);

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            organization = new
            {
                id = currentMembership.OrganizationId,
                name = currentMembership.Organization,
                number = currentMembership.OrganizationNumber
            },
            statement
        });
    }
}
