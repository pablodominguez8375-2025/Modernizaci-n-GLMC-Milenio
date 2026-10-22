using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// #635: el perfil institucional Comunicaciones publica el Boletín mensual
/// (sólo PDF, período obligatorio) y la revista, sin acceso a otras áreas.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class CommunicationsBulletinHttpTests
{
    [Fact]
    public async Task Communications_publishes_monthly_pdf_bulletin_and_nothing_else()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var objectStore = new InMemoryDocumentObjectStore();
        var scanner = new CleanDocumentMalwareScanner();
        using var root = new PmgmWebApplicationFactory(cs);
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.RemoveAll<IDocumentMalwareScanner>();
                services.AddSingleton<IDocumentObjectStore>(objectStore);
                services.AddSingleton<IDocumentMalwareScanner>(scanner);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = SyntheticRoleAuth.Name;
                    options.DefaultChallengeScheme = SyntheticRoleAuth.Name;
                    options.DefaultForbidScheme = SyntheticRoleAuth.Name;
                }).AddScheme<AuthenticationSchemeOptions, SyntheticRoleAuth>(SyntheticRoleAuth.Name, _ => { });
            }));

        var collection = new DocumentCollection
        {
            Code = "BOL-" + Guid.NewGuid().ToString("N")[..20],
            Name = "Publicaciones de la Orden (sintética)",
            Scope = DocumentManagementCodes.Scope.Order,
            Status = DocumentManagementCodes.CollectionStatus.Active,
            CreatedBySubject = "ci"
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
            var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            await documents.Database.MigrateAsync(ct);
            documents.DocumentCollections.Add(collection);
            await documents.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        void As(string role, string subject)
        {
            client.DefaultRequestHeaders.Remove("X-Role");
            client.DefaultRequestHeaders.Remove("X-Subject");
            client.DefaultRequestHeaders.Add("X-Role", role);
            client.DefaultRequestHeaders.Add("X-Subject", subject);
        }
        As(InstitutionalRoles.Communications, "comunicaciones-" + Guid.NewGuid().ToString("N"));

        // Sesión: capacidad propia, sin Biblioteca general ni Gestión Documental.
        var session = await client.GetFromJsonAsync<JsonElement>("/api/session/me", ct);
        var capabilities = session.GetProperty("capabilities");
        Assert.True(capabilities.GetProperty("canManageCommunications").GetBoolean());
        Assert.False(capabilities.GetProperty("canManageLibrary").GetBoolean());
        Assert.False(capabilities.GetProperty("canManageDocuments").GetBoolean());

        // Tipos no editoriales se rechazan; Gestión Documental queda fuera.
        using var badType = await client.PostAsJsonAsync("/api/biblioteca/editorial/revistas",
            new { collectionId = collection.Id, title = "Libro", documentType = "book" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        using var documentManager = await client.PostAsJsonAsync(
            $"/api/documentos/colecciones/{collection.Id}/documentos",
            new
            {
                title = "Libro por otra vía",
                documentType = "book",
                classification = DocumentManagementCodes.Classification.Internal,
                accessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated
            }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, documentManager.StatusCode);

        // Alta del Boletín como borrador.
        using var created = await client.PostAsJsonAsync("/api/biblioteca/editorial/revistas",
            new { collectionId = collection.Id, title = "Boletín de la Orden · Octubre 2026", documentType = "boletin" }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var documentId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("documentId").GetGuid();

        // Período obligatorio; se normaliza al primer día del mes.
        using var noPeriod = await client.PutAsJsonAsync($"/api/documentos/{documentId}/metadatos-biblioteca",
            new { shortDescription = "Resumen mensual" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, noPeriod.StatusCode);
        using var metadata = await client.PutAsJsonAsync($"/api/documentos/{documentId}/metadatos-biblioteca",
            new { documentDate = "2026-10-17", shortDescription = "Actividades de la Orden en octubre" }, ct);
        Assert.Equal(HttpStatusCode.OK, metadata.StatusCode);
        var metadataJson = await metadata.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("Bulletin", metadataJson.GetProperty("catalogKind").GetString());
        Assert.Equal("2026-10-01", metadataJson.GetProperty("documentDate").GetString());
        // Comunicaciones no fija el grado mínimo: es competencia del Bibliotecario.
        using var degree = await client.PutAsJsonAsync($"/api/documentos/{documentId}/metadatos-biblioteca",
            new { documentDate = "2026-10-01", minimumDegreeRequired = 3 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, degree.StatusCode);
        // Ni siquiera el Bibliotecario restringe por grado: revista y boletín son para todos (PO 22-10-2026).
        As(InstitutionalRoles.Librarian, "bibliotecario");
        using var librarianDegree = await client.PutAsJsonAsync($"/api/documentos/{documentId}/metadatos-biblioteca",
            new { documentDate = "2026-10-01", minimumDegreeRequired = 1 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, librarianDegree.StatusCode);
        As(InstitutionalRoles.Communications, "comunicaciones-" + Guid.NewGuid().ToString("N"));

        // Sólo PDF.
        using var word = await client.PostAsJsonAsync($"/api/documentos/{documentId}/versiones",
            new { originalFileName = "boletin.docx", contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", sizeBytes = 100 }, ct);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, word.StatusCode);

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% Boletin sintetico\n%%EOF\n");
        using var version = await client.PostAsJsonAsync($"/api/documentos/{documentId}/versiones",
            new { originalFileName = "boletin-2026-10.pdf", contentType = "application/pdf", sizeBytes = pdf.LongLength }, ct);
        Assert.Equal(HttpStatusCode.Created, version.StatusCode);
        var versionId = (await version.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("id").GetGuid();
        using var body = new ByteArrayContent(pdf);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using var upload = await client.PutAsync($"/api/documentos/versiones/{versionId}/contenido", body, ct);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        using var scan = await client.PostAsync($"/api/documentos/versiones/{versionId}/analizar", null, ct);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        // Listado editorial: aparece el Boletín con su tipo canónico.
        var list = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/editorial/revistas", ct);
        Assert.Contains(list.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("documentId").GetGuid() == documentId &&
                    item.GetProperty("documentType").GetString() == "bulletin");

        // El Gran Archivero no publica en Biblioteca.
        As(InstitutionalRoles.GrandArchivist, "archivero");
        using var archivist = await client.PostAsJsonAsync($"/api/biblioteca/editorial/revistas/{documentId}/publicar",
            new { versionId }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, archivist.StatusCode);

        // Publicación por Comunicaciones (otra persona del mismo perfil).
        As(InstitutionalRoles.Communications, "comunicaciones-otra");
        using var published = await client.PostAsJsonAsync($"/api/biblioteca/editorial/revistas/{documentId}/publicar",
            new { versionId }, ct);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        // Visible en el catálogo como categoría Boletines.
        As("reader", "hermano-lector");
        var search = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/buscar?documentType=boletines", ct);
        Assert.Contains(search.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == documentId);
        var facets = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/facetas", ct);
        Assert.Contains(facets.GetProperty("documentTypes").EnumerateArray(),
            item => item.GetProperty("value").GetString() == "bulletin" &&
                    item.GetProperty("label").GetString() == "Boletines");

        // Retiro reversible por Comunicaciones, con auditoría.
        As(InstitutionalRoles.Communications, "comunicaciones-otra");
        using var withdrawn = await client.PostAsync($"/api/biblioteca/editorial/revistas/{documentId}/retirar-publicacion", null, ct);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            var actions = await documents.AuditEvents.AsNoTracking()
                .Where(x => x.EntityId == documentId.ToString())
                .Select(x => x.Action).ToListAsync(ct);
            Assert.Contains("library.editorial.bulletin_draft.created", actions);
            Assert.Contains("library.editorial.bulletin_published", actions);
            Assert.Contains("library.editorial.bulletin_unpublished", actions);
        }
    }

    private sealed class SyntheticRoleAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "CommunicationsSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Role"].FirstOrDefault() ?? "reader";
            var subject = Request.Headers["X-Subject"].FirstOrDefault() ?? "reader";
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", subject),
                new Claim("name", subject),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            ], Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}
