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
/// Regla 22-10-2026 (#588/#635): corrección sólo por el perfil titular
/// (por cargo, no por persona) y reemplazo del archivo sin despublicar.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class LibraryPublicationReplacementHttpTests
{
    [Fact]
    public async Task Only_owner_profile_corrects_and_replacement_keeps_publication_visible()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var objectStore = new InMemoryDocumentObjectStore();
        using var root = new PmgmWebApplicationFactory(cs);
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.RemoveAll<IDocumentMalwareScanner>();
                services.AddSingleton<IDocumentObjectStore>(objectStore);
                services.AddSingleton<IDocumentMalwareScanner>(new CleanDocumentMalwareScanner());
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = SyntheticRoleAuth.Name;
                    options.DefaultChallengeScheme = SyntheticRoleAuth.Name;
                    options.DefaultForbidScheme = SyntheticRoleAuth.Name;
                }).AddScheme<AuthenticationSchemeOptions, SyntheticRoleAuth>(SyntheticRoleAuth.Name, _ => { });
            }));

        var collection = new DocumentCollection
        {
            Code = "REP-" + Guid.NewGuid().ToString("N")[..20],
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

        async Task<Guid> UploadVersionAsync(Guid documentId, string name)
        {
            var pdf = Encoding.ASCII.GetBytes($"%PDF-1.4\n% {name} {Guid.NewGuid()}\n%%EOF\n");
            using var version = await client.PostAsJsonAsync($"/api/documentos/{documentId}/versiones",
                new { originalFileName = name, contentType = "application/pdf", sizeBytes = pdf.LongLength }, ct);
            Assert.Equal(HttpStatusCode.Created, version.StatusCode);
            var id = (await version.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("id").GetGuid();
            using var body = new ByteArrayContent(pdf);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            using var upload = await client.PutAsync($"/api/documentos/versiones/{id}/contenido", body, ct);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            using var scan = await client.PostAsync($"/api/documentos/versiones/{id}/analizar", null, ct);
            Assert.Equal(HttpStatusCode.OK, scan.StatusCode);
            return id;
        }

        // Titular: Comunicaciones crea y publica la primera versión.
        As(InstitutionalRoles.Communications, "com-titular-anterior");
        using var created = await client.PostAsJsonAsync("/api/biblioteca/editorial/revistas",
            new { collectionId = collection.Id, title = "Boletín Octubre 2026", documentType = "boletin" }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var documentId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("documentId").GetGuid();
        using var meta = await client.PutAsJsonAsync($"/api/documentos/{documentId}/metadatos-biblioteca",
            new { documentDate = "2026-10-01", shortDescription = "Actividades de octubre" }, ct);
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);
        var v1 = await UploadVersionAsync(documentId, "boletin-v1.pdf");
        using var first = await client.PostAsJsonAsync(
            $"/api/biblioteca/editorial/revistas/{documentId}/publicar", new { versionId = v1 }, ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // El Bibliotecario no corrige lo publicado por Comunicaciones.
        As(InstitutionalRoles.Librarian, "bibliotecario");
        using var libMeta = await client.PutAsJsonAsync($"/api/documentos/{documentId}/metadatos-biblioteca",
            new { documentDate = "2026-10-01", shortDescription = "Intento ajeno" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, libMeta.StatusCode);
        using var libStage = await client.PostAsJsonAsync($"/api/documentos/{documentId}/versiones",
            new { originalFileName = "x.pdf", contentType = "application/pdf", sizeBytes = 10 }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, libStage.StatusCode);
        using var libWithdraw = await client.PostAsync(
            $"/api/biblioteca/editorial/revistas/{documentId}/retirar-publicacion", null, ct);
        Assert.Equal(HttpStatusCode.Forbidden, libWithdraw.StatusCode);
        var libList = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/editorial/revistas", ct);
        Assert.Contains(libList.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("documentId").GetGuid() == documentId &&
                    !item.GetProperty("canCorrect").GetBoolean());

        // Otra persona con el mismo cargo/perfil hereda y reemplaza sin despublicar.
        As(InstitutionalRoles.Communications, "com-titular-nuevo");
        var ownList = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/editorial/revistas", ct);
        Assert.Contains(ownList.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("documentId").GetGuid() == documentId &&
                    item.GetProperty("canCorrect").GetBoolean());
        var v2 = await UploadVersionAsync(documentId, "boletin-v2.pdf");

        // Sin motivo se rechaza; la versión vigente sigue publicada.
        using var noReason = await client.PostAsJsonAsync(
            $"/api/biblioteca/editorial/revistas/{documentId}/publicar", new { versionId = v2 }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        using var replaced = await client.PostAsJsonAsync(
            $"/api/biblioteca/editorial/revistas/{documentId}/publicar",
            new { versionId = v2, reason = "Se corrigió una fecha en la página 3." }, ct);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);

        await using var check = factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        var doc = await db.InstitutionalDocuments.AsNoTracking().SingleAsync(x => x.Id == documentId, ct);
        Assert.Equal(DocumentManagementCodes.DocumentStatus.Published, doc.Status);
        Assert.Equal(v2, doc.PublishedVersionId);
        Assert.Equal(LibraryPublisherProfiles.Communications, doc.PublisherProfile);
        Assert.Equal(2, await db.DocumentVersions.CountAsync(x => x.DocumentId == documentId, ct));
        var audit = await db.AuditEvents.AsNoTracking()
            .Where(x => x.EntityId == documentId.ToString() && x.Action == "library.editorial.bulletin_published")
            .ToListAsync(ct);
        Assert.Equal(2, audit.Count);
        Assert.Contains(audit, x => x.MetadataJson != null && x.MetadataJson.Contains("una fecha en la p"));
    }

    private sealed class SyntheticRoleAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "PublicationReplacementSynthetic";
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
