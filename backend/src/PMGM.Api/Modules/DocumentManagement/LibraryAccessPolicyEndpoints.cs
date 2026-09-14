using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

public static class LibraryAccessPolicyEndpoints
{
    public static IEndpointRouteBuilder MapLibraryAccessPolicyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
                "/api/documentos/{documentId:guid}/acceso-biblioteca/grado-minimo",
                SetMinimumDegreeAsync)
            .WithTags("Biblioteca Virtual")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> SetMinimumDegreeAsync(
        Guid documentId,
        SetLibraryMinimumDegreeRequest request,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        CancellationToken cancellationToken)
    {
        if (request.MinimumDegreeRequired is < 1 or > 99)
            return Results.BadRequest(new { message = "El grado mínimo debe estar entre 1 y 99 o ser nulo para quitar la restricción por grado." });

        var document = await db.InstitutionalDocuments
            .SingleOrDefaultAsync(x => x.Id == documentId, cancellationToken);
        if (document is null) return Results.NotFound();
        if (!access.CanManageDocuments(httpContext.User, document.OrganizationId)) return Results.Forbid();
        if (document.Status == DocumentManagementCodes.DocumentStatus.Retired)
            return Results.Conflict(new { message = "No se puede modificar la audiencia de un documento retirado." });

        var previous = document.MinimumDegreeRequired;
        document.MinimumDegreeRequired = request.MinimumDegreeRequired;

        if (previous != document.MinimumDegreeRequired)
        {
            db.AuditEvents.Add(AuditEventFactory.Create(
                httpContext,
                "library.minimum_degree.changed",
                nameof(InstitutionalDocument),
                document.Id.ToString(),
                document.OrganizationId,
                AuditResults.Success,
                new
                {
                    PreviousMinimumDegree = previous,
                    MinimumDegreeRequired = document.MinimumDegreeRequired,
                    document.Status
                }));
            await db.SaveChangesAsync(cancellationToken);
        }

        httpContext.Response.Headers.CacheControl = "private, no-store";
        return Results.Ok(new
        {
            document.Id,
            document.MinimumDegreeRequired,
            document.Status
        });
    }
}

public sealed record SetLibraryMinimumDegreeRequest(int? MinimumDegreeRequired);
