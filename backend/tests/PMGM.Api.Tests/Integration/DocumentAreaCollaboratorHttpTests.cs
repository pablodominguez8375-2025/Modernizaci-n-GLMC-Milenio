using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DocumentAreaCollaboratorHttpTests
{
    [Fact]
    public async Task Librarian_can_delegate_only_magazine_and_revoke_with_audit()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(cs);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        var before = await client.GetFromJsonAsync<JsonElement>(
            "/api/biblioteca/colaboradores", ct);
        var version = before.GetProperty("version").GetInt32();
        var subject = "comunicaciones-" + Guid.NewGuid().ToString("N");
        var payload = new
        {
            subject,
            category = "magazine",
            actions = new[] { "view", "create", "edit" },
            effectiveFrom = "2026-10-20",
            effectiveTo = "2026-12-31",
            expectedVersion = version
        };

        using var invalid = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores",
            new { subject, category = "revistas", actions = new[] { "view", "edit" },
                effectiveFrom = "2026-10-20", expectedVersion = version }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var otherArea = await client.PostAsJsonAsync(
            "/api/documentos/colaboradores", payload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, otherArea.StatusCode);
        using var created = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores", payload, ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var assignmentId = json.GetProperty("assignmentId").GetGuid();
        Assert.Equal(version + 1, json.GetProperty("version").GetInt32());

        using var stale = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores", payload, ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>(
            "/api/biblioteca/colaboradores", ct);
        Assert.Contains(list.GetProperty("items").EnumerateArray(),
            entry => entry.GetProperty("assignmentId").GetGuid() == assignmentId);
        var nextVersion = list.GetProperty("version").GetInt32();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
            var today = new DateOnly(2026, 10, 20);
            Assert.True(DynamicAccessEndpoints.IsCategoryGranted(
                catalog, subject, "library", "edit", "magazine", null, today));
            Assert.False(DynamicAccessEndpoints.IsCategoryGranted(
                catalog, subject, "library", "edit", "book", null, today));
            Assert.False(DynamicAccessEndpoints.IsCategoryGranted(
                catalog, subject, "documentmanager", "edit", "magazine", null, today));
            Assert.False(DynamicAccessEndpoints.IsGranted(
                catalog, subject, "library", "edit", null, today));
        }

        using var revoked = await client.DeleteAsync(
            $"/api/biblioteca/colaboradores/{assignmentId}?expectedVersion={nextVersion}", ct);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
            Assert.False(DynamicAccessEndpoints.IsCategoryGranted(
                catalog, subject, "library", "edit", "magazine", null,
                new DateOnly(2026, 10, 20)));
            Assert.True(await db.AuditEvents.AsNoTracking().AnyAsync(
                x => x.Action == "library.collaborator.assigned" &&
                     x.EntityId == assignmentId.ToString("D"), ct));
            Assert.True(await db.AuditEvents.AsNoTracking().AnyAsync(
                x => x.Action == "library.collaborator.revoked" &&
                     x.EntityId == assignmentId.ToString("D"), ct));
        }
    }

    [Fact]
    public async Task Archivist_may_delegate_only_document_manager_and_collaborator_may_not_delegate()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(cs);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.GrandArchivist);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/biblioteca/colaboradores", ct)).StatusCode);
        var catalog = await client.GetFromJsonAsync<JsonElement>(
            "/api/documentos/colaboradores", ct);
        var version = catalog.GetProperty("version").GetInt32();
        using var created = await client.PostAsJsonAsync(
            "/api/documentos/colaboradores",
            new
            {
                subject = "archivo-" + Guid.NewGuid().ToString("N"),
                category = "official_document",
                actions = new[] { "view" },
                effectiveFrom = "2026-10-20",
                expectedVersion = version
            }, ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", "collaborator");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/documentos/colaboradores", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/documentos/colaboradores",
                new
                {
                    subject = "otro",
                    category = "official_document",
                    actions = new[] { "view" },
                    effectiveFrom = "2026-10-20",
                    expectedVersion = version + 1
                }, ct)).StatusCode);
    }

    [Fact]
    public async Task Magazine_metadata_editor_is_limited_to_granted_document_category_and_revocation()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(cs);
        var orderCollection = new DocumentCollection
        {
            Code = "REV-RBAC-" + Guid.NewGuid().ToString("N"),
            Name = "Revistas CI", Scope = DocumentManagementCodes.Scope.Order,
            Status = DocumentManagementCodes.CollectionStatus.Active,
            CreatedBySubject = "ci"
        };
        var workshopId = Guid.NewGuid();
        var workshopCollection = new DocumentCollection
        {
            Code = "REV-LOCAL-" + Guid.NewGuid().ToString("N"),
            Name = "Revistas Taller CI", Scope = DocumentManagementCodes.Scope.Organization,
            OrganizationId = workshopId,
            Status = DocumentManagementCodes.CollectionStatus.Active,
            CreatedBySubject = "ci"
        };
        var magazine = Document(orderCollection, "revista");
        var book = Document(orderCollection, "book");
        var confidential = Document(orderCollection, "magazine",
            DocumentManagementCodes.Classification.Confidential);
        var workshopMagazine = Document(workshopCollection, "magazine", organizationId: workshopId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var institutionalDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await institutionalDb.Database.MigrateAsync(ct);
            // La FK documental exige que el Taller sintético exista en el
            // padrón institucional antes de crear su colección de prueba.
            institutionalDb.Organizations.Add(new Organization
            {
                Id = workshopId,
                Name = "Taller RBAC sintético " + workshopId.ToString("N"),
                Type = "workshop"
            });
            await institutionalDb.SaveChangesAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            await db.Database.MigrateAsync(ct);
            db.DocumentCollections.AddRange(orderCollection, workshopCollection);
            db.InstitutionalDocuments.AddRange(magazine, book, confidential, workshopMagazine);
            await db.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        var before = await client.GetFromJsonAsync<JsonElement>(
            "/api/biblioteca/colaboradores", ct);
        var subject = "editor-revistas-" + Guid.NewGuid().ToString("N");
        using var grant = await client.PostAsJsonAsync("/api/biblioteca/colaboradores",
            new
            {
                subject,
                category = "magazine",
                actions = new[] { "view", "edit" },
                effectiveFrom = DynamicAccessEndpoints.Today().ToString("yyyy-MM-dd"),
                effectiveTo = DynamicAccessEndpoints.Today().AddDays(30).ToString("yyyy-MM-dd"),
                expectedVersion = before.GetProperty("version").GetInt32()
            }, ct);
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        var created = await grant.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var assignmentId = created.GetProperty("assignmentId").GetGuid();
        var version = created.GetProperty("version").GetInt32();

        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", "collaborator");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", subject);
        var payload = new
        {
            minimumDegreeRequired = (int?)null,
            authorName = "Equipo revista",
            topic = "Cultura",
            edition = "N° 3",
            shortDescription = "Actualización autorizada"
        };
        using var editableList = await client.GetAsync(
            "/api/biblioteca/editorial/revistas?page=1&pageSize=50", ct);
        Assert.Equal(HttpStatusCode.OK, editableList.StatusCode);
        Assert.True(editableList.Headers.CacheControl?.NoStore);
        Assert.True(editableList.Headers.CacheControl?.Private);
        var editorial = await editableList.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: ct);
        Assert.Equal(1, editorial.GetProperty("page").GetInt32());
        var visibleIds = editorial.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("documentId").GetGuid()).ToArray();
        Assert.Contains(magazine.Id, visibleIds);
        Assert.DoesNotContain(book.Id, visibleIds);
        Assert.DoesNotContain(confidential.Id, visibleIds);
        Assert.DoesNotContain(workshopMagazine.Id, visibleIds);
        Assert.DoesNotContain("createdBySubject", editorial.ToString(),
            StringComparison.OrdinalIgnoreCase);
        using var invalidPage = await client.GetAsync(
            "/api/biblioteca/editorial/revistas?pageSize=1000", ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);

        using var edited = await client.PutAsJsonAsync(
            $"/api/documentos/{magazine.Id}/metadatos-biblioteca", payload, ct);
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using var gradeChange = await client.PutAsJsonAsync(
            $"/api/documentos/{magazine.Id}/metadatos-biblioteca",
            new
            {
                minimumDegreeRequired = 3,
                authorName = "Equipo revista",
                edition = "N° 4"
            }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, gradeChange.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>()
                .InstitutionalDocuments.AsNoTracking().SingleAsync(d => d.Id == magazine.Id, ct);
            Assert.Equal("N° 3", saved.Edition);
            Assert.Equal("Equipo revista", saved.AuthorName);
        }

        foreach (var rejected in new[] { book, confidential, workshopMagazine })
        {
            using var denied = await client.PutAsJsonAsync(
                $"/api/documentos/{rejected.Id}/metadatos-biblioteca", payload, ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var noVersions = await client.PostAsJsonAsync(
            $"/api/documentos/{magazine.Id}/versiones",
            new { originalFileName = "no-permitido.pdf", contentType = "application/pdf", sizeBytes = 123 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noVersions.StatusCode);
        using var noPublish = await client.PostAsJsonAsync(
            $"/api/documentos/{magazine.Id}/publicar", new { versionId = Guid.NewGuid() }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noPublish.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Collab-Subject");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", "otro-actor");
        using var wrongSubject = await client.PutAsJsonAsync(
            $"/api/documentos/{magazine.Id}/metadatos-biblioteca", payload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongSubject.StatusCode);
        using var wrongList = await client.GetAsync(
            "/api/biblioteca/editorial/revistas", ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongList.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Collab-Subject");
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        using var revoked = await client.DeleteAsync(
            $"/api/biblioteca/colaboradores/{assignmentId}?expectedVersion={version}", ct);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", "collaborator");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", subject);
        using var deniedAfterRevoke = await client.PutAsJsonAsync(
            $"/api/documentos/{magazine.Id}/metadatos-biblioteca", payload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAfterRevoke.StatusCode);
        using var deniedListAfterRevoke = await client.GetAsync(
            "/api/biblioteca/editorial/revistas", ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedListAfterRevoke.StatusCode);
    }

    [Fact]
    public async Task Magazine_draft_creation_requires_create_grant_and_real_order_collection()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory(cs);
        var active = new DocumentCollection
        {
            Code = "BIB-NEW-" + Guid.NewGuid().ToString("N"),
            Name = "Revistas Orden", Scope = DocumentManagementCodes.Scope.Order,
            Status = DocumentManagementCodes.CollectionStatus.Active,
            CreatedBySubject = "ci"
        };
        var inactive = new DocumentCollection
        {
            Code = "BIB-OLD-" + Guid.NewGuid().ToString("N"),
            Name = "Retiradas Orden", Scope = DocumentManagementCodes.Scope.Order,
            Status = DocumentManagementCodes.CollectionStatus.Retired,
            CreatedBySubject = "ci"
        };
        var lodgeId = Guid.NewGuid();
        var lodge = new DocumentCollection
        {
            Code = "BIB-LDG-" + Guid.NewGuid().ToString("N"),
            Name = "Revistas Taller", Scope = DocumentManagementCodes.Scope.Organization,
            OrganizationId = lodgeId,
            Status = DocumentManagementCodes.CollectionStatus.Active,
            CreatedBySubject = "ci"
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var primary = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await primary.Database.MigrateAsync(ct);
            primary.Organizations.Add(new Organization
            {
                Id = lodgeId, Name = "Taller sintético editorial " + lodgeId.ToString("N"),
                Type = "workshop"
            });
            await primary.SaveChangesAsync(ct);
            var docs = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            await docs.Database.MigrateAsync(ct);
            docs.DocumentCollections.AddRange(active, inactive, lodge);
            await docs.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        var catalog = await client.GetFromJsonAsync<JsonElement>(
            "/api/biblioteca/colaboradores", ct);
        var who = "creator-" + Guid.NewGuid().ToString("N");
        using var granted = await client.PostAsJsonAsync("/api/biblioteca/colaboradores",
            new
            {
                subject = who,
                category = "magazine",
                actions = new[] { "view", "create" },
                effectiveFrom = DynamicAccessEndpoints.Today().ToString("yyyy-MM-dd"),
                effectiveTo = DynamicAccessEndpoints.Today().AddDays(30).ToString("yyyy-MM-dd"),
                expectedVersion = catalog.GetProperty("version").GetInt32()
            }, ct);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        var grantData = await granted.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var grantId = grantData.GetProperty("assignmentId").GetGuid();
        var nextVersion = grantData.GetProperty("version").GetInt32();

        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", "collaborator");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", who);

        var createPayload = new { collectionId = active.Id, title = "Revista CI " + Guid.NewGuid().ToString("N") };
        using var created = await client.PostAsJsonAsync(
            "/api/biblioteca/editorial/revistas", createPayload, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var id = body.GetProperty("documentId").GetGuid();
        Assert.Equal(DocumentManagementCodes.DocumentStatus.Draft, body.GetProperty("status").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var docs = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            var saved = await docs.InstitutionalDocuments.AsNoTracking()
                .SingleAsync(x => x.Id == id, ct);
            Assert.Equal(active.Id, saved.CollectionId);
            Assert.Null(saved.OrganizationId);
            Assert.Equal("magazine", saved.DocumentType);
            Assert.Equal(DocumentManagementCodes.Classification.Internal, saved.Classification);
            Assert.Equal(DocumentManagementCodes.AccessPolicy.LibraryAuthenticated, saved.AccessPolicy);
            Assert.Equal(DocumentManagementCodes.DocumentStatus.Draft, saved.Status);
            Assert.Null(saved.PublishedVersionId);
            Assert.False(await docs.DocumentVersions.AnyAsync(x => x.DocumentId == id, ct));
            Assert.True(await docs.AuditEvents.AnyAsync(
                x => x.Action == "library.editorial.magazine_draft.created" &&
                     x.EntityId == id.ToString("D"), ct));
        }

        foreach (var collection in new[] { inactive.Id, lodge.Id })
        {
            using var denied = await client.PostAsJsonAsync(
                "/api/biblioteca/editorial/revistas",
                new { collectionId = collection, title = "No autorizado" }, ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var badTitle = await client.PostAsJsonAsync(
            "/api/biblioteca/editorial/revistas", new { collectionId = active.Id, title = "  " }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, badTitle.StatusCode);
        using var noEdit = await client.PutAsJsonAsync(
            $"/api/documentos/{id}/metadatos-biblioteca",
            new { minimumDegreeRequired = (int?)null, authorName = "Sin editar" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noEdit.StatusCode);
        using var noVersion = await client.PostAsJsonAsync(
            $"/api/documentos/{id}/versiones",
            new { originalFileName = "sin-subida.pdf", contentType = "application/pdf", sizeBytes = 123 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noVersion.StatusCode);
        using var noPublish = await client.PostAsJsonAsync(
            $"/api/documentos/{id}/publicar", new { versionId = Guid.NewGuid() }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noPublish.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Collab-Subject");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", "other-subject");
        using var other = await client.PostAsJsonAsync(
            "/api/biblioteca/editorial/revistas", createPayload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Collab-Subject");
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        using var revoked = await client.DeleteAsync(
            $"/api/biblioteca/colaboradores/{grantId}?expectedVersion={nextVersion}", ct);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", "collaborator");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", who);
        using var afterRevoked = await client.PostAsJsonAsync(
            "/api/biblioteca/editorial/revistas", createPayload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, afterRevoked.StatusCode);
    }

    [Fact]
    public async Task Magazine_write_grant_can_upload_and_scan_own_version_but_never_publish_or_fake_scan()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryDocumentObjectStore();
        var scanner = new CleanDocumentMalwareScanner();
        using var root = Factory(cs);
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.RemoveAll<IDocumentMalwareScanner>();
                services.AddSingleton<IDocumentObjectStore>(store);
                services.AddSingleton<IDocumentMalwareScanner>(scanner);
            }));

        var collection = new DocumentCollection
        {
            Code = "WRITE-MAG-" + Guid.NewGuid().ToString("N"),
            Name = "Revistas de prueba", Scope = DocumentManagementCodes.Scope.Order,
            Status = DocumentManagementCodes.CollectionStatus.Active,
            CreatedBySubject = "ci"
        };
        var magazine = Document(collection, "magazine");
        var book = Document(collection, "book");
        var confidential = Document(collection, "magazine",
            DocumentManagementCodes.Classification.Confidential);
        var priorVersion = new DocumentVersion
        {
            Document = magazine,
            DocumentId = magazine.Id,
            VersionNumber = 1,
            OriginalFileName = "otro-autor.txt",
            ContentType = "text/plain",
            SizeBytes = 4,
            ObjectKey = DocumentObjectKeyFactory.Create(magazine.Id, Guid.NewGuid()),
            ProcessingStatus = DocumentManagementCodes.ProcessingStatus.PendingUpload,
            CreatedBySubject = "otro-actor"
        };
        priorVersion.ObjectKey = DocumentObjectKeyFactory.Create(magazine.Id, priorVersion.Id);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var institutionalDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await institutionalDb.Database.MigrateAsync(ct);
            var docs = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            await docs.Database.MigrateAsync(ct);
            docs.DocumentCollections.Add(collection);
            docs.InstitutionalDocuments.AddRange(magazine, book, confidential);
            docs.DocumentVersions.Add(priorVersion);
            await docs.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        var listing = await client.GetFromJsonAsync<JsonElement>(
            "/api/biblioteca/colaboradores", ct);
        var version = listing.GetProperty("version").GetInt32();
        using var invalidCategory = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores",
            new
            {
                subject = "prohibido-" + Guid.NewGuid().ToString("N"),
                category = "book",
                actions = new[] { "view", "write" },
                effectiveFrom = DynamicAccessEndpoints.Today().ToString("yyyy-MM-dd"),
                expectedVersion = version
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCategory.StatusCode);

        var user = "revista-writer-" + Guid.NewGuid().ToString("N");
        using var granted = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores",
            new
            {
                subject = user,
                category = "magazine",
                actions = new[] { "view", "write" },
                effectiveFrom = DynamicAccessEndpoints.Today().ToString("yyyy-MM-dd"),
                effectiveTo = DynamicAccessEndpoints.Today().AddDays(15).ToString("yyyy-MM-dd"),
                expectedVersion = version
            }, ct);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        var grant = await granted.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var assignmentId = grant.GetProperty("assignmentId").GetGuid();
        var grantedVersion = grant.GetProperty("version").GetInt32();
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", "collaborator");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", user);

        using var noCreate = await client.PostAsJsonAsync(
            "/api/biblioteca/editorial/revistas",
            new { collectionId = collection.Id, title = "No crear" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noCreate.StatusCode);
        using var noEdit = await client.PutAsJsonAsync(
            $"/api/documentos/{magazine.Id}/metadatos-biblioteca",
            new { minimumDegreeRequired = (int?)null, authorName = "No editar" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noEdit.StatusCode);
        foreach (var deniedId in new[] { book.Id, confidential.Id })
        {
            using var denied = await client.PostAsJsonAsync(
                $"/api/documentos/{deniedId}/versiones",
                new { originalFileName = "no-permitido.txt", contentType = "text/plain", sizeBytes = 5 },
                ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        var bytes = Encoding.UTF8.GetBytes("Contenido revisable y seguro de revista");
        using var reservation = await client.PostAsJsonAsync(
            $"/api/documentos/{magazine.Id}/versiones",
            new
            {
                originalFileName = "revista-qa.txt",
                contentType = "text/plain",
                sizeBytes = bytes.LongLength
            }, ct);
        Assert.Equal(HttpStatusCode.Created, reservation.StatusCode);
        var reserved = await reservation.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var newVersionId = reserved.GetProperty("id").GetGuid();
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.PendingUpload,
            reserved.GetProperty("processingStatus").GetString());

        using var stolen = new ByteArrayContent(bytes);
        stolen.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var notOwnerUpload = await client.PutAsync(
            $"/api/documentos/versiones/{priorVersion.Id}/contenido", stolen, ct);
        Assert.Equal(HttpStatusCode.Forbidden, notOwnerUpload.StatusCode);

        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var uploaded = await client.PutAsync(
            $"/api/documentos/versiones/{newVersionId}/contenido", content, ct);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        Assert.True(store.ContainsDocumentVersion(newVersionId));
        using var fakeTransition = await client.PostAsJsonAsync(
            $"/api/documentos/versiones/{newVersionId}/estado",
            new { targetStatus = DocumentManagementCodes.ProcessingStatus.Available,
                sha256 = new string('a', 64), scanReference = "user-faked" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, fakeTransition.StatusCode);
        using var scanned = await client.PostAsync(
            $"/api/documentos/versiones/{newVersionId}/analizar", null, ct);
        Assert.Equal(HttpStatusCode.OK, scanned.StatusCode);
        Assert.Equal(1, scanner.ScanCount);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var docs = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            var checkedVersion = await docs.DocumentVersions.AsNoTracking()
                .SingleAsync(x => x.Id == newVersionId, ct);
            Assert.Equal(user, checkedVersion.CreatedBySubject);
            Assert.Equal(DocumentManagementCodes.ProcessingStatus.Available,
                checkedVersion.ProcessingStatus);
            Assert.StartsWith("clamav:test-clean:", checkedVersion.ScanReference,
                StringComparison.Ordinal);
            var checkedDocument = await docs.InstitutionalDocuments.AsNoTracking()
                .SingleAsync(x => x.Id == magazine.Id, ct);
            Assert.Equal(DocumentManagementCodes.DocumentStatus.Draft, checkedDocument.Status);
            Assert.Null(checkedDocument.PublishedVersionId);
        }
        using var noPublish = await client.PostAsJsonAsync(
            $"/api/documentos/{magazine.Id}/publicar",
            new { versionId = newVersionId }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noPublish.StatusCode);
        using var noEditorialPublish = await client.PostAsJsonAsync(
            $"/api/biblioteca/editorial/revistas/{magazine.Id}/publicar",
            new { versionId = newVersionId }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, noEditorialPublish.StatusCode);
        using var noManagedDownload = await client.GetAsync(
            $"/api/documentos/versiones/{newVersionId}/contenido", ct);
        Assert.Equal(HttpStatusCode.Forbidden, noManagedDownload.StatusCode);

        using var otherReserved = await client.PostAsJsonAsync(
            $"/api/documentos/{magazine.Id}/versiones",
            new { originalFileName = "pendiente.txt", contentType = "text/plain", sizeBytes = 4 }, ct);
        Assert.Equal(HttpStatusCode.Created, otherReserved.StatusCode);
        var pending = await otherReserved.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var pendingId = pending.GetProperty("id").GetGuid();

        client.DefaultRequestHeaders.Remove("X-Collab-Subject");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", "otro-actor");
        using var wrongActor = new ByteArrayContent(Encoding.UTF8.GetBytes("hola"));
        wrongActor.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var wrongActorUpload = await client.PutAsync(
            $"/api/documentos/versiones/{pendingId}/contenido", wrongActor, ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongActorUpload.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Collab-Subject");
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        using var revoked = await client.DeleteAsync(
            $"/api/biblioteca/colaboradores/{assignmentId}?expectedVersion={grantedVersion}", ct);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", "collaborator");
        client.DefaultRequestHeaders.Add("X-Collab-Subject", user);
        using var deniedAfter = new ByteArrayContent(Encoding.UTF8.GetBytes("hola"));
        deniedAfter.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        using var deniedUpload = await client.PutAsync(
            $"/api/documentos/versiones/{pendingId}/contenido", deniedAfter, ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedUpload.StatusCode);
        using var deniedScan = await client.PostAsync(
            $"/api/documentos/versiones/{newVersionId}/analizar", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedScan.StatusCode);

        // Sólo el Bibliotecario puede aprobar la publicación, y únicamente
        // después del escaneo y de comparar la huella con el objeto real.
        client.DefaultRequestHeaders.Remove("X-Collab-Subject");
        client.DefaultRequestHeaders.Remove("X-Collab-Role");
        client.DefaultRequestHeaders.Add("X-Collab-Role", InstitutionalRoles.Librarian);
        using var pendingPublish = await client.PostAsJsonAsync(
            $"/api/biblioteca/editorial/revistas/{magazine.Id}/publicar",
            new { versionId = pendingId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, pendingPublish.StatusCode);
        foreach (var excluded in new[] { book.Id, confidential.Id })
        {
            using var forbiddenPublish = await client.PostAsJsonAsync(
                $"/api/biblioteca/editorial/revistas/{excluded}/publicar",
                new { versionId = newVersionId }, ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenPublish.StatusCode);
        }

        using var published = await client.PostAsJsonAsync(
            $"/api/biblioteca/editorial/revistas/{magazine.Id}/publicar",
            new { versionId = newVersionId }, ct);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.True(published.Headers.CacheControl?.NoStore);
        var publication = await published.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(DocumentManagementCodes.DocumentStatus.Published,
            publication.GetProperty("status").GetString());
        Assert.Equal(newVersionId,
            publication.GetProperty("publishedVersionId").GetGuid());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var docs = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            var publishedDoc = await docs.InstitutionalDocuments.AsNoTracking()
                .SingleAsync(x => x.Id == magazine.Id, ct);
            Assert.Equal(DocumentManagementCodes.DocumentStatus.Published, publishedDoc.Status);
            Assert.Equal(newVersionId, publishedDoc.PublishedVersionId);
            Assert.True(await docs.AuditEvents.AnyAsync(
                x => x.Action == "library.editorial.magazine_published" &&
                     x.EntityId == magazine.Id.ToString("D"), ct));
        }
    }

    private static InstitutionalDocument Document(
        DocumentCollection collection,
        string type,
        string classification = DocumentManagementCodes.Classification.Internal,
        Guid? organizationId = null)
        => new()
        {
            CollectionId = collection.Id,
            Collection = collection,
            OrganizationId = organizationId,
            Title = "Publicación de prueba " + Guid.NewGuid().ToString("N"),
            DocumentType = type,
            Classification = classification,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            Status = DocumentManagementCodes.DocumentStatus.Draft,
            CreatedBySubject = "ci"
        };

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory(string cs)
    {
        var root = new PmgmWebApplicationFactory(cs);
        return root.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SyntheticAuth.Name;
                options.DefaultChallengeScheme = SyntheticAuth.Name;
                options.DefaultForbidScheme = SyntheticAuth.Name;
            }).AddScheme<AuthenticationSchemeOptions, SyntheticAuth>(
                SyntheticAuth.Name, _ => { })));
    }

    private sealed class SyntheticAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "DocumentCollaboratorSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Collab-Role"].ToString();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", Request.Headers["X-Collab-Subject"].FirstOrDefault() ?? "actor-test"),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            ], Name));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, Name)));
        }
    }
}
