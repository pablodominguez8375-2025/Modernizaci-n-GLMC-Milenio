using System.Net;
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

/// <summary>#588 — las planchas quedan «para revisión»: invisibles hasta que el Bibliotecario aprueba o devuelve con motivo.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class WorkPaperReviewHttpTests
{
    [Fact]
    public async Task Librarian_approves_or_returns_pending_work_papers_and_nobody_else_can()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryDocumentObjectStore();
        using var root = new PmgmWebApplicationFactory(cs);
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.AddSingleton<IDocumentObjectStore>(store);
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = SyntheticRoleAuth.Name;
                    o.DefaultChallengeScheme = SyntheticRoleAuth.Name;
                    o.DefaultForbidScheme = SyntheticRoleAuth.Name;
                }).AddScheme<AuthenticationSchemeOptions, SyntheticRoleAuth>(SyntheticRoleAuth.Name, _ => { });
            }));

        var collection = new DocumentCollection
        {
            Code = "WP-" + Guid.NewGuid().ToString("N")[..20], Name = "Planchas (sintética)",
            Scope = DocumentManagementCodes.Scope.Order, CreatedBySubject = "ci"
        };
        var approveId = await SeedPendingAsync(factory, store, collection, "Plancha a aprobar", ct);
        var returnId = await SeedPendingAsync(factory, store, collection, "Plancha a devolver", ct);

        using var client = factory.CreateClient();
        void As(string role)
        {
            client.DefaultRequestHeaders.Remove("X-Role");
            client.DefaultRequestHeaders.Add("X-Role", role);
        }

        // Quien no es Bibliotecario no ve ni resuelve nada (ni siquiera Comunicaciones o el admin de Gran Logia).
        foreach (var role in new[] { InstitutionalRoles.Communications, InstitutionalRoles.GranLogiaAdmin })
        {
            As(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/biblioteca/revision", ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/biblioteca/revision/{approveId}/aprobar", null, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/biblioteca/revision/{approveId}/devolver", new { reason = "Motivo suficientemente largo" }, ct)).StatusCode);
        }

        As(InstitutionalRoles.Librarian);
        var pending = (await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/revision", ct)).GetProperty("items");
        Assert.Contains(pending.EnumerateArray(), i => i.GetProperty("id").GetGuid() == approveId);
        Assert.Contains(pending.EnumerateArray(), i => i.GetProperty("id").GetGuid() == returnId);

        var download = await client.GetAsync($"/api/biblioteca/revision/{approveId}/contenido", ct);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("contenido-de-la-plancha", await download.Content.ReadAsStringAsync(ct));

        // Devolver exige motivo.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/biblioteca/revision/{returnId}/devolver", new { reason = "corto" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/biblioteca/revision/{returnId}/devolver", new { reason = "Falta la portada con el nombre del Taller." }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/biblioteca/revision/{returnId}/aprobar", null, ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/biblioteca/revision/{approveId}/aprobar", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/biblioteca/revision/{approveId}/aprobar", null, ct)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        var approved = await db.InstitutionalDocuments.AsNoTracking().SingleAsync(x => x.Id == approveId, ct);
        Assert.Equal(WorkPaperReviewStatus.Approved, approved.ReviewStatus);
        Assert.Equal(DocumentManagementCodes.DocumentStatus.Published, approved.Status);
        Assert.NotNull(approved.PublishedVersionId);
        var returned = await db.InstitutionalDocuments.AsNoTracking().SingleAsync(x => x.Id == returnId, ct);
        Assert.Equal(WorkPaperReviewStatus.Returned, returned.ReviewStatus);
        Assert.Equal(DocumentManagementCodes.DocumentStatus.Draft, returned.Status);
        Assert.Null(returned.PublishedVersionId);
        Assert.Equal("Falta la portada con el nombre del Taller.", returned.ReviewReason);
        Assert.True(await db.AuditEvents.AnyAsync(x => x.EntityId == approveId.ToString() && x.Action == "library.work_paper.approved", ct));
        Assert.True(await db.AuditEvents.AnyAsync(x => x.EntityId == returnId.ToString() && x.Action == "library.work_paper.returned", ct));

        // Resueltas, ya no están pendientes ni se pueden descargar por la vía de revisión.
        var after = (await client.GetFromJsonAsync<JsonElement>("/api/biblioteca/revision", ct)).GetProperty("items");
        Assert.DoesNotContain(after.EnumerateArray(), i => i.GetProperty("id").GetGuid() == approveId || i.GetProperty("id").GetGuid() == returnId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/biblioteca/revision/{approveId}/contenido", ct)).StatusCode);
    }

    private static async Task<Guid> SeedPendingAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, InMemoryDocumentObjectStore store,
        DocumentCollection collection, string title, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
        var db = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        await db.Database.MigrateAsync(ct);
        if (!await db.DocumentCollections.AnyAsync(x => x.Code == collection.Code, ct))
        {
            db.DocumentCollections.Add(collection);
            await db.SaveChangesAsync(ct);
        }
        var bytes = Encoding.UTF8.GetBytes("contenido-de-la-plancha");
        var integrity = await DocumentContentIntegrity.ComputeAsync(new MemoryStream(bytes), ct);
        var document = new InstitutionalDocument
        {
            CollectionId = collection.Id, OrganizationId = collection.OrganizationId,
            Title = title, DocumentType = "work_paper", Classification = DocumentManagementCodes.Classification.Confidential,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.OrganizationAuthenticated, MinimumDegreeRequired = 2,
            AuthorName = "Autor Sintético", AuthorLodgeName = "Taller Sintético", ShortDescription = "Descripción breve",
            Status = DocumentManagementCodes.DocumentStatus.Draft, CreatedBySubject = "ci"
        };
        var version = new DocumentVersion
        {
            DocumentId = document.Id, VersionNumber = 1, OriginalFileName = "plancha.pdf", ContentType = "application/pdf",
            SizeBytes = integrity.SizeBytes, Sha256 = integrity.Sha256, AuthorEffectiveDegreeAtUpload = 2,
            ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Available, ScanReference = "clamav:test-clean:ci",
            SubmittedTitle = title, SubmittedShortDescription = "Descripción breve", CreatedBySubject = "ci"
        };
        version.ObjectKey = DocumentObjectKeyFactory.Create(document.Id, version.Id);
        document.ReviewStatus = WorkPaperReviewStatus.Pending;
        document.ReviewVersionId = version.Id;
        document.ReviewSubmittedAtUtc = DateTimeOffset.UtcNow;
        db.InstitutionalDocuments.Add(document);
        db.DocumentVersions.Add(version);
        await db.SaveChangesAsync(ct);
        await store.StoreAsync(version.ObjectKey, new MemoryStream(bytes), "application/pdf", ct);
        return document.Id;
    }

    private sealed class SyntheticRoleAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "WorkPaperReviewSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Role"].FirstOrDefault() ?? "reader";
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", role), new Claim("name", role),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            ], Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}
