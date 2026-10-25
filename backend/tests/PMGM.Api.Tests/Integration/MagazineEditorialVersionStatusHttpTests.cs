using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class MagazineEditorialVersionStatusHttpTests
{
    [Fact]
    public async Task Status_lists_only_own_versions_and_never_leaks_scan_secrets()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new PmgmWebApplicationFactory(cs);
        using var factory = root.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = SyntheticStatusAuth.Name;
                    options.DefaultChallengeScheme = SyntheticStatusAuth.Name;
                    options.DefaultForbidScheme = SyntheticStatusAuth.Name;
                }).AddScheme<AuthenticationSchemeOptions, SyntheticStatusAuth>(
                    SyntheticStatusAuth.Name, _ => { })));

        var collection = new DocumentCollection
        {
            Code = "REV-STATUS-" + Guid.NewGuid().ToString("N"),
            Name = "Revista institucional sintética",
            Scope = DocumentManagementCodes.Scope.Order,
            Status = DocumentManagementCodes.CollectionStatus.Active,
            CreatedBySubject = "ci"
        };
        var magazine = NewDocument(collection, "magazine");
        var book = NewDocument(collection, "book");
        var confidential = NewDocument(collection, "magazine");
        confidential.Classification = DocumentManagementCodes.Classification.Confidential;
        var ownedVersion = NewVersion(magazine, "editor", 1);
        var otherVersion = NewVersion(magazine, "other", 2);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var institutional = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await institutional.Database.MigrateAsync(ct);
            var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            await documents.Database.MigrateAsync(ct);
            documents.DocumentCollections.Add(collection);
            documents.InstitutionalDocuments.AddRange(magazine, book, confidential);
            documents.DocumentVersions.AddRange(ownedVersion, otherVersion);
            await documents.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Status-Role", InstitutionalRoles.Librarian);
        var listing = await client.GetFromJsonAsync<JsonElement>(
            "/api/biblioteca/colaboradores", ct);
        var actor = "editor-" + Guid.NewGuid().ToString("N");
        // The persisted subject must match the owner of the reserved version.
        ownedVersion.CreatedBySubject = actor;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            var stored = await documents.DocumentVersions.SingleAsync(v => v.Id == ownedVersion.Id, ct);
            stored.CreatedBySubject = actor;
            await documents.SaveChangesAsync(ct);
        }
        using var assigned = await client.PostAsJsonAsync(
            "/api/biblioteca/colaboradores",
            new
            {
                subject = actor,
                category = "magazine",
                actions = new[] { "view", "write" },
                effectiveFrom = DynamicAccessEndpoints.Today().ToString("yyyy-MM-dd"),
                effectiveTo = DynamicAccessEndpoints.Today().AddDays(10).ToString("yyyy-MM-dd"),
                expectedVersion = listing.GetProperty("version").GetInt32()
            }, ct);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var granted = await assigned.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        client.DefaultRequestHeaders.Remove("X-Status-Role");
        client.DefaultRequestHeaders.Add("X-Status-Role", "collaborator");
        client.DefaultRequestHeaders.Add("X-Status-Subject", actor);

        using var response = await client.GetAsync(
            $"/api/biblioteca/editorial/revistas/{magazine.Id}/versiones", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        Assert.Equal(ownedVersion.Id, body.GetProperty("items")[0].GetProperty("id").GetGuid());
        foreach (var key in new[] { "sha256", "scanReference", "objectKey", "createdBySubject" })
            Assert.DoesNotContain(key, body.ToString(), StringComparison.OrdinalIgnoreCase);

        foreach (var forbidden in new[] { book.Id, confidential.Id })
        {
            using var denied = await client.GetAsync(
                $"/api/biblioteca/editorial/revistas/{forbidden}/versiones", ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var invalid = await client.GetAsync(
            $"/api/biblioteca/editorial/revistas/{magazine.Id}/versiones?pageSize=150", ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Status-Subject");
        client.DefaultRequestHeaders.Add("X-Status-Subject", "not-owner");
        using var foreign = await client.GetAsync(
            $"/api/biblioteca/editorial/revistas/{magazine.Id}/versiones", ct);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Status-Role");
        client.DefaultRequestHeaders.Add("X-Status-Role", InstitutionalRoles.Librarian);
        using var librarian = await client.GetAsync(
            $"/api/biblioteca/editorial/revistas/{magazine.Id}/versiones", ct);
        Assert.Equal(HttpStatusCode.OK, librarian.StatusCode);
        var all = await librarian.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(2, all.GetProperty("total").GetInt32());
        using var revoked = await client.DeleteAsync(
            $"/api/biblioteca/colaboradores/{granted.GetProperty("assignmentId").GetGuid()}?expectedVersion={granted.GetProperty("version").GetInt32()}", ct);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Status-Role");
        client.DefaultRequestHeaders.Add("X-Status-Role", "collaborator");
        client.DefaultRequestHeaders.Remove("X-Status-Subject");
        client.DefaultRequestHeaders.Add("X-Status-Subject", actor);
        using var deniedAfterRevoke = await client.GetAsync(
            $"/api/biblioteca/editorial/revistas/{magazine.Id}/versiones", ct);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAfterRevoke.StatusCode);
    }

    private static InstitutionalDocument NewDocument(DocumentCollection collection, string kind)
        => new()
        {
            Collection = collection, CollectionId = collection.Id,
            DocumentType = kind, Title = "Sintético " + kind,
            Classification = DocumentManagementCodes.Classification.Internal,
            AccessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated,
            Status = DocumentManagementCodes.DocumentStatus.Draft,
            CreatedBySubject = "ci"
        };

    private static DocumentVersion NewVersion(InstitutionalDocument document, string subject, int number)
    {
        var v = new DocumentVersion
        {
            Document = document, DocumentId = document.Id,
            VersionNumber = number, OriginalFileName = "test.txt",
            ContentType = "text/plain", SizeBytes = 32,
            ProcessingStatus = DocumentManagementCodes.ProcessingStatus.PendingUpload,
            CreatedBySubject = subject
        };
        v.ObjectKey = DocumentObjectKeyFactory.Create(document.Id, v.Id);
        return v;
    }

    private sealed class SyntheticStatusAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "MagazineStatusSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Status-Role"].ToString();
            var subject = Request.Headers["X-Status-Subject"].FirstOrDefault() ?? "reader";
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", subject),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, role)
            ], Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
        }
    }
}
