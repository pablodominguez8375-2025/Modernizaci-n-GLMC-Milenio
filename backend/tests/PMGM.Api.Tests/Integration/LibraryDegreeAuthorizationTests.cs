using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LibraryDegreeAuthorizationTests
{
    private const string TestIssuer = "urn:pmgm:unspecified-issuer";
    private const string TestSubject = "ci-document-admin";

    [Fact]
    public async Task Library_enforces_accumulated_degree_in_search_facets_detail_and_download()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var objectStore = new InMemoryDocumentObjectStore();
        var scanner = new CleanDocumentMalwareScanner();

        using var baseFactory = new DocumentManagementWebApplicationFactory(connectionString);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentObjectStore>();
                services.RemoveAll<IDocumentMalwareScanner>();
                services.AddSingleton<IDocumentObjectStore>(objectStore);
                services.AddSingleton<IDocumentMalwareScanner>(scanner);
            }));
        using var client = factory.CreateClient();

        Guid memberId;
        Guid organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM core.member_identity_links WHERE \"Issuer\" = {TestIssuer} AND \"Subject\" = {TestSubject}",
                cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Biblioteca Grado {Guid.NewGuid():N}"[..40],
                Number = Guid.NewGuid().ToString("N")[..8],
                Type = "workshop"
            };
            var person = new Person
            {
                FirstNames = "Hermano",
                LastNames = "Prueba Biblioteca",
                Email = $"biblioteca-{Guid.NewGuid():N}@example.test"
            };
            var member = new Member
            {
                Person = person,
                PersonId = person.Id,
                InstitutionalNumber = $"BIB-{Guid.NewGuid():N}"[..20]
            };

            db.Organizations.Add(organization);
            db.People.Add(person);
            db.Members.Add(member);
            db.DegreeEvents.Add(new DegreeEvent
            {
                Member = member,
                MemberId = member.Id,
                Organization = organization,
                OrganizationId = organization.Id,
                Degree = "1",
                EventType = MembershipCodes.DegreeEvent.Initiation,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)
            });
            await db.SaveChangesAsync(cancellationToken);

            memberId = member.Id;
            organizationId = organization.Id;
            var linkId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links
                    ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject", "RevokedAtUtc")
                VALUES
                    ({linkId}, {member.Id}, {TestIssuer}, {TestSubject}, {createdAt}, {TestSubject}, NULL)
                """, cancellationToken);
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var collectionId = await CreateCollectionAsync(client, suffix, cancellationToken);
        var commonNeedle = $"grado-{suffix}";
        var type1 = $"grado1_{suffix}";
        var type2 = $"grado2_{suffix}";
        var type3 = $"grado3_{suffix}";

        var doc1 = await CreatePublishedTextDocumentAsync(
            client, collectionId, $"{commonNeedle} documento uno", type1, 1, cancellationToken);
        var doc2 = await CreatePublishedTextDocumentAsync(
            client, collectionId, $"{commonNeedle} documento dos", type2, 2, cancellationToken);
        var doc3 = await CreatePublishedTextDocumentAsync(
            client, collectionId, $"{commonNeedle} documento tres", type3, 3, cancellationToken);

        var grade1Search = await GetSearchTextAsync(client, commonNeedle, cancellationToken);
        Assert.Contains(doc1.Id.ToString(), grade1Search, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(doc2.Id.ToString(), grade1Search, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(doc3.Id.ToString(), grade1Search, StringComparison.OrdinalIgnoreCase);

        var grade1Facets = await client.GetStringAsync("/api/biblioteca/facetas", cancellationToken);
        Assert.Contains(type1, grade1Facets, StringComparison.Ordinal);
        Assert.DoesNotContain(type2, grade1Facets, StringComparison.Ordinal);
        Assert.DoesNotContain(type3, grade1Facets, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/biblioteca/{doc2.Id}", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/biblioteca/{doc2.Id}/contenido", cancellationToken)).StatusCode);

        await AddDegreeAsync(factory, memberId, organizationId, 2, MembershipCodes.DegreeEvent.WageIncrease, -1, cancellationToken);

        var grade2Search = await GetSearchTextAsync(client, commonNeedle, cancellationToken);
        Assert.Contains(doc1.Id.ToString(), grade2Search, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(doc2.Id.ToString(), grade2Search, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(doc3.Id.ToString(), grade2Search, StringComparison.OrdinalIgnoreCase);

        var grade2Detail = await client.GetAsync($"/api/biblioteca/{doc2.Id}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, grade2Detail.StatusCode);
        var grade2Download = await client.GetAsync($"/api/biblioteca/{doc2.Id}/contenido", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, grade2Download.StatusCode);
        Assert.Equal(doc2.Payload, await grade2Download.Content.ReadAsByteArrayAsync(cancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/biblioteca/{doc3.Id}", cancellationToken)).StatusCode);

        await AddDegreeAsync(factory, memberId, organizationId, 3, MembershipCodes.DegreeEvent.Exaltation, 0, cancellationToken);

        var grade3Search = await GetSearchTextAsync(client, commonNeedle, cancellationToken);
        Assert.Contains(doc1.Id.ToString(), grade3Search, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(doc2.Id.ToString(), grade3Search, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(doc3.Id.ToString(), grade3Search, StringComparison.OrdinalIgnoreCase);

        var grade3Detail = await client.GetAsync($"/api/biblioteca/{doc3.Id}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, grade3Detail.StatusCode);
        var grade3Download = await client.GetAsync($"/api/biblioteca/{doc3.Id}/contenido", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, grade3Download.StatusCode);
        Assert.Equal(doc3.Payload, await grade3Download.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    private static async Task<Guid> CreateCollectionAsync(
        HttpClient client,
        string suffix,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/documentos/colecciones",
            new
            {
                code = $"GRADE-{suffix}",
                name = $"Biblioteca grados {suffix}",
                description = "Prueba de autorización acumulativa por grado",
                scope = DocumentManagementCodes.Scope.Order,
                organizationId = (Guid?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return json.GetProperty("id").GetGuid();
    }

    private static async Task<PublishedDocument> CreatePublishedTextDocumentAsync(
        HttpClient client,
        Guid collectionId,
        string title,
        string documentType,
        int minimumDegree,
        CancellationToken cancellationToken)
    {
        var documentResponse = await client.PostAsJsonAsync(
            $"/api/documentos/colecciones/{collectionId}/documentos",
            new
            {
                title,
                documentType,
                classification = DocumentManagementCodes.Classification.Internal,
                accessPolicy = DocumentManagementCodes.AccessPolicy.LibraryAuthenticated
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, documentResponse.StatusCode);
        var documentJson = await documentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var documentId = documentJson.GetProperty("id").GetGuid();

        var payload = Encoding.UTF8.GetBytes($"Contenido protegido {documentType}");
        var versionResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/versiones",
            new
            {
                originalFileName = $"{documentType}.txt",
                contentType = "text/plain",
                sizeBytes = payload.LongLength
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);
        var versionJson = await versionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var versionId = versionJson.GetProperty("id").GetGuid();

        using var uploadContent = new ByteArrayContent(payload);
        uploadContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var uploadResponse = await client.PutAsync(
            $"/api/documentos/versiones/{versionId}/contenido",
            uploadContent,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var scanResponse = await client.PostAsync(
            $"/api/documentos/versiones/{versionId}/analizar",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, scanResponse.StatusCode);

        var publishResponse = await client.PostAsJsonAsync(
            $"/api/documentos/{documentId}/publicar",
            new { versionId },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        var degreeResponse = await client.PutAsJsonAsync(
            $"/api/documentos/{documentId}/acceso-biblioteca/grado-minimo",
            new { minimumDegreeRequired = minimumDegree },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, degreeResponse.StatusCode);

        return new PublishedDocument(documentId, payload);
    }

    private static async Task<string> GetSearchTextAsync(
        HttpClient client,
        string needle,
        CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(
            $"/api/biblioteca/buscar?q={Uri.EscapeDataString(needle)}&pageSize=10",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static async Task AddDegreeAsync(
        WebApplicationFactory<Program> factory,
        Guid memberId,
        Guid organizationId,
        int degree,
        string eventType,
        int effectiveDayOffset,
        CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        db.DegreeEvents.Add(new DegreeEvent
        {
            MemberId = memberId,
            OrganizationId = organizationId,
            Degree = degree.ToString(),
            EventType = eventType,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(effectiveDayOffset)
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record PublishedDocument(Guid Id, byte[] Payload);
}
