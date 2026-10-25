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

/// <summary>#588 — el Bibliotecario carga y publica libros y categorías propias; nadie más.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class LibraryManagedPublicationHttpTests
{
    [Fact]
    public async Task Librarian_uploads_and_publishes_books_and_custom_categories_and_nobody_else_can()
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
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
            await scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>().Database.MigrateAsync(ct);
        }
        using var client = factory.CreateClient();
        void As(string role, string subject)
        {
            client.DefaultRequestHeaders.Remove("X-Role");
            client.DefaultRequestHeaders.Remove("X-Subject");
            client.DefaultRequestHeaders.Add("X-Role", role);
            client.DefaultRequestHeaders.Add("X-Subject", subject);
        }
        var book = new { categoryCode = "book", title = "Historia de la masonería", authorName = "Autor Sintético", topic = "Historia", edition = "1ª", description = "Abstract del libro sintético.", minimumDegreeRequired = 2 };

        // Nadie más carga por esta vía.
        foreach (var role in new[] { InstitutionalRoles.Communications, InstitutionalRoles.GranLogiaAdmin, InstitutionalRoles.GrandArchivist })
        {
            As(role, "otro-" + role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/biblioteca/gestion/publicaciones", book, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/biblioteca/gestion/publicaciones", ct)).StatusCode);
        }

        As(InstitutionalRoles.Librarian, "bibliotecario-" + Guid.NewGuid().ToString("N"));
        // Validaciones: categoría desconocida/no gestionada, libro incompleto, grado inválido.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/biblioteca/gestion/publicaciones", new { categoryCode = "no_existe", title = "X" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/biblioteca/gestion/publicaciones", new { categoryCode = "magazine", title = "X" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/biblioteca/gestion/publicaciones", new { categoryCode = "work_paper", title = "X" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/biblioteca/gestion/publicaciones", new { categoryCode = "book", title = "Sin datos" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/biblioteca/gestion/publicaciones", new { book.categoryCode, book.title, book.authorName, book.topic, book.edition, book.description, minimumDegreeRequired = 7 }, ct)).StatusCode);

        // Categoría propia, luego desactivada: no admite cargas.
        var created = await client.PostAsJsonAsync("/api/biblioteca/categorias", new { label = "Manuales " + Guid.NewGuid().ToString("N")[..6] }, ct);
        var custom = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("value").GetString()!;

        using var draft = await client.PostAsJsonAsync("/api/biblioteca/gestion/publicaciones", book, ct);
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var bookId = (await draft.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("documentId").GetGuid();
        using var customDraft = await client.PostAsJsonAsync("/api/biblioteca/gestion/publicaciones", new { categoryCode = custom, title = "Manual de oficio" }, ct);
        Assert.Equal(HttpStatusCode.Created, customDraft.StatusCode);

        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% Libro sintetico\n%%EOF\n");
        using var version = await client.PostAsJsonAsync($"/api/documentos/{bookId}/versiones",
            new { originalFileName = "libro.pdf", contentType = "application/pdf", sizeBytes = pdf.LongLength }, ct);
        Assert.Equal(HttpStatusCode.Created, version.StatusCode);
        var versionId = (await version.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();

        // Otro perfil no puede reservar ni cargar sobre ese borrador.
        As(InstitutionalRoles.Communications, "comunicaciones");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/documentos/{bookId}/versiones",
            new { originalFileName = "otro.pdf", contentType = "application/pdf", sizeBytes = pdf.LongLength }, ct)).StatusCode);

        As(InstitutionalRoles.Librarian, "bibliotecario-2");
        using var body = new ByteArrayContent(pdf);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        // La versión la reservó otro sujeto del mismo perfil: la carga exige haberla reservado (regla vigente).
        As(InstitutionalRoles.Librarian, "bibliotecario-" + Guid.NewGuid().ToString("N"));
        using var version2 = await client.PostAsJsonAsync($"/api/documentos/{bookId}/versiones",
            new { originalFileName = "libro-v2.pdf", contentType = "application/pdf", sizeBytes = pdf.LongLength }, ct);
        Assert.Equal(HttpStatusCode.Created, version2.StatusCode);
        var v2 = (await version2.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
        using var upload = await client.PutAsync($"/api/documentos/versiones/{v2}/contenido", body, ct);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/documentos/versiones/{v2}/analizar", null, ct)).StatusCode);

        // Publica el Bibliotecario; Comunicaciones y el Gran Archivero no.
        As(InstitutionalRoles.Communications, "comunicaciones");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/biblioteca/editorial/revistas/{bookId}/publicar", new { versionId = v2 }, ct)).StatusCode);
        As(InstitutionalRoles.Librarian, "bibliotecario-3");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/biblioteca/editorial/revistas/{bookId}/publicar", new { versionId = v2 }, ct)).StatusCode);

        // Visible para un hermano en la categoría Libros, con el grado elegido.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            var doc = await documents.InstitutionalDocuments.AsNoTracking().SingleAsync(x => x.Id == bookId, ct);
            Assert.Equal(DocumentManagementCodes.DocumentStatus.Published, doc.Status);
            Assert.Equal(2, doc.MinimumDegreeRequired);
            Assert.Equal(LibraryPublisherProfiles.Librarian, doc.PublisherProfile);
            var actions = await documents.AuditEvents.AsNoTracking().Where(x => x.EntityId == bookId.ToString()).Select(x => x.Action).ToListAsync(ct);
            Assert.Contains("library.managed.book_draft.created", actions);
            Assert.Contains("library.editorial.book_published", actions);
        }
        var list = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/gestion/publicaciones", ct);
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == bookId);
        As("reader", "hermano-lector");
        var search = await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/buscar?documentType=book", ct);
        Assert.DoesNotContain(search.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == bookId);
    }

    private sealed class SyntheticRoleAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "ManagedPublicationSynthetic";
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
