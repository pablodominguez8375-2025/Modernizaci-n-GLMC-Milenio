using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement.Entities;

namespace PMGM.Api.Modules.DocumentManagement;

public static class DocumentContentRecoveryEndpoints
{
    public static IEndpointRouteBuilder MapDocumentContentRecoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/documentos/versiones/{versionId:guid}/reconciliar-contenido",
                ReconcileContentAsync)
            .WithTags("Contenido Documental")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> ReconcileContentAsync(
        Guid versionId,
        HttpContext httpContext,
        DocumentManagementDbContext db,
        IInstitutionalAccessService access,
        IDocumentObjectStore objectStore,
        CancellationToken cancellationToken)
    {
        var version = await db.DocumentVersions
            .Include(x => x.Document)
            .SingleOrDefaultAsync(x => x.Id == versionId, cancellationToken);

        if (version is null) return Results.NotFound(new { message = "La versión indicada no existe." });
        if (!access.CanManageDocuments(httpContext.User, version.Document.OrganizationId)) return Results.Forbid();

        if (version.ProcessingStatus != DocumentManagementCodes.ProcessingStatus.PendingUpload)
        {
            return Results.Conflict(new
            {
                message = "La reconciliación sólo aplica a versiones pendientes cuya carga física pudo quedar incompleta a nivel transaccional."
            });
        }

        if (!await objectStore.ExistsAsync(version.ObjectKey, cancellationToken))
        {
            return Results.NotFound(new
            {
                message = "No existe un objeto físico pendiente que pueda reconciliarse."
            });
        }

        if (!DocumentContentTypePolicy.TryValidateMetadata(
                version.OriginalFileName,
                version.ContentType,
                out var expectedContentType,
                out var metadataError))
        {
            await objectStore.DeleteAsync(version.ObjectKey, cancellationToken);
            await RecordRejectedRecoveryAsync(
                httpContext,
                db,
                version,
                "documents.content.reconciliation_invalid_metadata",
                cancellationToken);

            return Results.Json(
                new
                {
                    message = metadataError ?? "Los metadatos del objeto pendiente no son válidos; el objeto físico fue eliminado."
                },
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        bool contentTypeMatches;
        await using (var content = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken))
        {
            contentTypeMatches = await DocumentContentTypePolicy.MatchesContentAsync(
                content,
                expectedContentType,
                cancellationToken);
        }

        if (!contentTypeMatches)
        {
            await objectStore.DeleteAsync(version.ObjectKey, cancellationToken);
            await RecordRejectedRecoveryAsync(
                httpContext,
                db,
                version,
                "documents.content.reconciliation_signature_rejected",
                cancellationToken);

            return Results.Json(
                new { message = "El objeto pendiente no corresponde al tipo documental declarado y fue eliminado." },
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        DocumentContentIntegrityResult integrity;
        await using (var content = await objectStore.OpenReadAsync(version.ObjectKey, cancellationToken))
        {
            integrity = await DocumentContentIntegrity.ComputeAsync(content, cancellationToken);
        }

        if (integrity.SizeBytes != version.SizeBytes)
        {
            await objectStore.DeleteAsync(version.ObjectKey, cancellationToken);
            await RecordRejectedRecoveryAsync(
                httpContext,
                db,
                version,
                "documents.content.reconciliation_size_rejected",
                cancellationToken);

            return Results.Conflict(new
            {
                message = "El objeto pendiente tiene un tamaño inconsistente y fue eliminado para permitir una nueva carga."
            });
        }

        var transition = DocumentVersionLifecycle.CanTransition(
            version.ProcessingStatus,
            DocumentManagementCodes.ProcessingStatus.Uploaded,
            integrity.Sha256,
            version.ScanReference);

        if (!transition.Allowed)
            return Results.Conflict(new { message = transition.Error });

        version.Sha256 = integrity.Sha256;
        version.ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Uploaded;

        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            "documents.content.reconciled",
            nameof(DocumentVersion),
            version.Id.ToString(),
            version.Document.OrganizationId,
            AuditResults.Success,
            new
            {
                version.DocumentId,
                version.VersionNumber,
                SizeBytes = integrity.SizeBytes,
                version.ProcessingStatus,
                HasIntegrityHash = true,
                RecoveredFromOrphanedObject = true
            }));

        await db.SaveChangesAsync(cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";

        return Results.Ok(new
        {
            version.Id,
            version.DocumentId,
            version.VersionNumber,
            version.ProcessingStatus,
            SizeBytes = integrity.SizeBytes,
            HasIntegrityHash = true,
            Reconciled = true
        });
    }

    private static async Task RecordRejectedRecoveryAsync(
        HttpContext httpContext,
        DocumentManagementDbContext db,
        DocumentVersion version,
        string action,
        CancellationToken cancellationToken)
    {
        db.AuditEvents.Add(AuditEventFactory.Create(
            httpContext,
            action,
            nameof(DocumentVersion),
            version.Id.ToString(),
            version.Document.OrganizationId,
            AuditResults.Rejected,
            new
            {
                version.DocumentId,
                version.VersionNumber,
                ObjectDeleted = true,
                version.ProcessingStatus
            }));

        await db.SaveChangesAsync(cancellationToken);
    }
}
