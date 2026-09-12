using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.DocumentManagement;

public sealed class LibraryDegreeAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        DocumentManagementDbContext db,
        IInstitutionalMemberContextResolver memberContextResolver)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (string.Equals(path.TrimEnd('/'), "/api/biblioteca", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/api/biblioteca/buscar", permanent: false, preserveMethod: true);
            return;
        }

        if (!TryGetLibraryDocumentId(path, out var documentId))
        {
            await next(context);
            return;
        }

        var document = await db.InstitutionalDocuments
            .AsNoTracking()
            .Where(x => x.Id == documentId)
            .Select(x => new { x.Id, x.MinimumDegreeRequired })
            .SingleOrDefaultAsync(context.RequestAborted);

        if (document is null || document.MinimumDegreeRequired is null)
        {
            await next(context);
            return;
        }

        var memberContext = await memberContextResolver.ResolveAsync(context.User, context.RequestAborted);
        if (memberContext is null || memberContext.EffectiveDegree < document.MinimumDegreeRequired.Value)
        {
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    }

    private static bool TryGetLibraryDocumentId(string path, out Guid documentId)
    {
        documentId = Guid.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length is not (3 or 4)) return false;
        if (!string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(segments[1], "biblioteca", StringComparison.OrdinalIgnoreCase)) return false;
        if (segments.Length == 4 &&
            !string.Equals(segments[3], "contenido", StringComparison.OrdinalIgnoreCase)) return false;

        return Guid.TryParse(segments[2], out documentId);
    }
}
