using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.DocumentManagement;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DocumentManagementHttpWorkflowTests
{
    private const string ShaV1 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ShaV2 = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public async Task Document_versions_are_immutable_and_library_exposes_only_minimized_metadata()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new DocumentManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var collectionResponse = await client.PostAsJsonAsync(
            "/api/documentos/colecciones",
            new
            {
                code = $"BIB-CI-{Guid.NewGuid():N}"[..24],
                name = "Biblioteca CI",
                description = "Colección de integración",
                scope = DocumentManagementCodes.Scope.Order,
                organizationId = (Guid?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, collectionResponse.StatusCode);
        var collectionJson = await collectionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var collectionId = collectionJson.GetProperty("id").GetGuid();

        var documentResponse = await client.PostAsJsonAsync(
            $"/api/documentos/colecciones/{collectionId}/documentos",
            new
            {
                title = "Documento institucional CI",
                documentType = "historical_publication",
                classification = DocumentManagementCodes.Classification.Internal,
                accessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, documentResponse.StatusCode);
        var documentJson = await documentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var documentId = documentJson.GetProperty("id").GetGuid();

        var versionV1Response = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/versiones",
            new { originalFileName = "documento-interno-v1.pdf", contentType = "application/pdf", sizeBytes = 12345 },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, versionV1Response.StatusCode);
        var versionV1Json = await versionV1Response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var versionV1Id = versionV1Json.GetProperty("id").GetGuid();
        Assert.Equal(1, versionV1Json.GetProperty("versionNumber").GetInt32());
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.PendingUpload, versionV1Json.GetProperty("processingStatus").GetString());

        var prematurePublish = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/publicar",
            new { versionId = versionV1Id },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, prematurePublish.StatusCode);

        await TransitionAsync(client, versionV1Id, DocumentManagementCodes.ProcessingStatus.Uploaded, ShaV1, null, cancellationToken);
        await TransitionAsync(client, versionV1Id, DocumentManagementCodes.ProcessingStatus.Scanning, null, null, cancellationToken);
        await TransitionAsync(client, versionV1Id, DocumentManagementCodes.ProcessingStatus.Available, null, "scan-ci-v1-clean", cancellationToken);

        var publishV1 = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/publicar",
            new { versionId = versionV1Id },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publishV1.StatusCode);

        var versionV2Response = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/versiones",
            new { originalFileName = "documento-interno-v2.pdf", contentType = "application/pdf", sizeBytes = 15000 },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, versionV2Response.StatusCode);
        var versionV2Json = await versionV2Response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var versionV2Id = versionV2Json.GetProperty("id").GetGuid();
        Assert.Equal(2, versionV2Json.GetProperty("versionNumber").GetInt32());

        await TransitionAsync(client, versionV2Id, DocumentManagementCodes.ProcessingStatus.Uploaded, ShaV2, null, cancellationToken);
        await TransitionAsync(client, versionV2Id, DocumentManagementCodes.ProcessingStatus.Scanning, null, null, cancellationToken);
        await TransitionAsync(client, versionV2Id, DocumentManagementCodes.ProcessingStatus.Available, null, "scan-ci-v2-clean", cancellationToken);

        var publishV2 = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/publicar",
            new { versionId = versionV2Id },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publishV2.StatusCode);

        var libraryResponse = await client.GetAsync("/api/biblioteca", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, libraryResponse.StatusCode);
        var libraryText = await libraryResponse.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("Documento institucional CI", libraryText);
        Assert.DoesNotContain("objectKey", libraryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sha256", libraryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("originalFileName", libraryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scanReference", libraryText, StringComparison.OrdinalIgnoreCase);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var documentDb = verificationScope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        var versions = await documentDb.DocumentVersions.AsNoTracking()
            .Where(x => x.DocumentId == documentId)
            .OrderBy(x => x.VersionNumber)
            .ToListAsync(cancellationToken);
        Assert.Equal(2, versions.Count);
        Assert.Equal(ShaV1, versions[0].Sha256);
        Assert.Equal(ShaV2, versions[1].Sha256);
        Assert.NotEqual(versions[0].ObjectKey, versions[1].ObjectKey);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Available, versions[0].ProcessingStatus);
        Assert.Equal(DocumentManagementCodes.ProcessingStatus.Available, versions[1].ProcessingStatus);

        var persistedDocument = await documentDb.InstitutionalDocuments.AsNoTracking()
            .SingleAsync(x => x.Id == documentId, cancellationToken);
        Assert.Equal(versionV2Id, persistedDocument.PublishedVersionId);
        Assert.Equal(DocumentManagementCodes.DocumentStatus.Published, persistedDocument.Status);

        var auditMetadata = await documentDb.AuditEvents.AsNoTracking()
            .Where(x => x.EntityId == documentId.ToString() || x.EntityId == versionV1Id.ToString() || x.EntityId == versionV2Id.ToString())
            .Select(x => x.MetadataJson)
            .ToListAsync(cancellationToken);
        var combinedAudit = string.Join("\n", auditMetadata.Where(x => x is not null));
        Assert.DoesNotContain("documento-interno", combinedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ShaV1, combinedAudit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scan-ci", combinedAudit, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task TransitionAsync(
        HttpClient client,
        Guid versionId,
        string targetStatus,
        string? sha256,
        string? scanReference,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/documentos/versiones/{versionId}/estado",
            new { targetStatus, sha256, scanReference },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

internal sealed class DocumentManagementWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>();
            services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.RemoveAll<DocumentManagementDbContext>();
            services.RemoveAll<DbContextOptions<DocumentManagementDbContext>>();
            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<DocumentManagementDbContext>(options => options.UseNpgsql(connectionString));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = DocumentManagementTestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = DocumentManagementTestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = DocumentManagementTestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, DocumentManagementTestAuthenticationHandler>(
                    DocumentManagementTestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }
}

internal sealed class DocumentManagementTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PMGM-Documents-CI-Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("sub", "ci-document-admin"),
            new Claim(ClaimTypes.NameIdentifier, "ci-document-admin"),
            new Claim(ClaimTypes.Name, "CI Document Admin"),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
