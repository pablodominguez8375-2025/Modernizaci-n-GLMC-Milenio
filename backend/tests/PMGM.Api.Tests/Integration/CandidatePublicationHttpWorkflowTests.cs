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
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidatePublicationHttpWorkflowTests
{
    private static readonly byte[] PhotoBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");

    [Fact]
    public async Task Portal_is_transversal_minimized_and_excludes_non_current_publications()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var member = factory.CreateClient();
        var seeds = await SeedAsync(factory, ct);
        Authenticate(member, seeds.OtherWorkshopId);

        var response = await member.GetAsync("/api/candidate-publications/active", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var items = json.GetProperty("items").EnumerateArray().ToArray();
        foreach (var seed in seeds.Publications)
        {
            var matches = items.Where(x => x.GetProperty("displayName").GetString() == seed.DisplayName).ToArray();
            if (!seed.IsCurrent)
            {
                Assert.Empty(matches);
                var hiddenPhoto = await member.GetAsync($"/api/candidate-publications/{seed.PublicationId}/photo", ct);
                Assert.Equal(HttpStatusCode.NotFound, hiddenPhoto.StatusCode);
                continue;
            }
            var item = Assert.Single(matches);
            Assert.Equal(seeds.WorkshopName, item.GetProperty("workshopName").GetString());
            Assert.Equal(seeds.WorkshopNumber, item.GetProperty("workshopNumber").GetString());
            var fields = item.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
            Assert.True(fields.SetEquals(new[] { "displayName", "workshopName", "workshopNumber",
                "publishedFromUtc", "publishedUntilUtc", "requiredDays", "elapsedDays", "complianceDateUtc",
                "ruleCode", "status", "photoUrl" }));
            Assert.DoesNotContain("PRIVATE-", item.GetRawText(), StringComparison.Ordinal);
            if (seed.HasPhoto)
                Assert.Equal($"/api/candidate-publications/{seed.PublicationId:D}/photo", item.GetProperty("photoUrl").GetString());
            else
                Assert.Equal(JsonValueKind.Null, item.GetProperty("photoUrl").ValueKind);
        }
    }

    [Fact]
    public async Task Anonymous_is_rejected_and_photo_access_stops_when_publication_expires()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var seeds = await SeedAsync(factory, ct);
        var seed = seeds.Publications[0];
        var photoUrl = $"/api/candidate-publications/{seed.PublicationId}/photo";

        foreach (var url in new[] { "/api/candidate-publications/active", photoUrl,
            $"/api/insinuados/solicitudes/{seed.RequestId}/ficha", $"/api/insinuados/solicitudes/{seed.RequestId}/foto" })
        {
            var anonymous = await client.GetAsync(url, ct);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        Authenticate(client, seeds.OtherWorkshopId);
        var photo = await client.GetAsync(photoUrl, ct);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PhotoBytes, await photo.Content.ReadAsByteArrayAsync(ct));
        Assert.Equal("private, no-store", photo.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", Assert.Single(photo.Headers.GetValues("X-Content-Type-Options")));
        Assert.DoesNotContain("PRIVATE-", photo.Headers.ToString() + photo.Content.Headers, StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var publication = await db.CandidatePublications.SingleAsync(x => x.Id == seed.PublicationId, ct);
            publication.PublishedUntilUtc = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync(ct);
        }
        var expired = await client.GetAsync(photoUrl, ct);
        Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        var list = await client.GetAsync("/api/candidate-publications/active", ct);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.DoesNotContain(seed.DisplayName, await list.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_photo_does_not_grant_private_file_access_even_to_another_workshop_secretary()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var seeds = await SeedAsync(factory, ct);
        var seed = seeds.Publications[0];
        var privateUrls = new[] { $"/api/insinuados/solicitudes/{seed.RequestId}/ficha",
            $"/api/insinuados/solicitudes/{seed.RequestId}/foto", $"/api/documentos/versiones/{seed.VersionId}/contenido" };
        foreach (var (organization, role) in new[] {
            (seeds.OtherWorkshopId, "member"), (seeds.WorkshopId, "member"),
            (seeds.OtherWorkshopId, InstitutionalRoles.TallerSecretaria) })
        {
            Authenticate(client, organization, role);
            foreach (var url in privateUrls)
            {
                var denied = await client.GetAsync(url, ct);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                Assert.DoesNotContain("PRIVATE-", await denied.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
            }
        }
        Authenticate(client, seeds.WorkshopId, InstitutionalRoles.TallerSecretaria);
        var profile = await client.GetAsync(privateUrls[0], ct);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var json = await profile.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("PRIVATE-ID", json.GetProperty("rutOrInstitutionalId").GetString());
        var privatePhoto = await client.GetAsync(privateUrls[1], ct);
        Assert.Equal(HttpStatusCode.OK, privatePhoto.StatusCode);
        Assert.Equal(PhotoBytes, await privatePhoto.Content.ReadAsByteArrayAsync(ct));
    }

    private static void Authenticate(HttpClient client, Guid organization, string role = "member")
    {
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Organization");
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", organization.ToString());
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", role);
    }

    private sealed record PublicationSeed(Guid PublicationId, Guid RequestId, Guid VersionId,
        string DisplayName, bool IsCurrent, bool HasPhoto);
    private sealed record Seed(Guid WorkshopId, Guid OtherWorkshopId, string WorkshopName,
        string WorkshopNumber, PublicationSeed[] Publications);

    private static async Task<Seed> SeedAsync(CandidatePublicationTestFactory factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var intake = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
        var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        await db.Database.MigrateAsync(ct);
        await intake.Database.MigrateAsync(ct);
        await documents.Database.MigrateAsync(ct);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workshop = new Organization { Name = $"Taller ficticio {suffix}", Number = suffix, Type = "workshop" };
        var other = new Organization { Name = $"Otro Taller ficticio {suffix}", Number = $"B-{suffix}", Type = "workshop" };
        db.AddRange(workshop, other);
        var result = new List<PublicationSeed>();
        for (var i = 0; i < 5; i++)
        {
            var person = new Person { FirstNames = $"Persona ficticia {suffix}", LastNames = $"Apellido {i}",
                Phone = "PRIVATE-PHONE", Email = "PRIVATE-EMAIL", Address = "PRIVATE-ADDRESS" };
            var ceremony = new CeremonyRequest { Organization = workshop, OrganizationId = workshop.Id,
                CandidatePerson = person, CandidatePersonId = person.Id, CeremonyType = CeremonyCodes.Type.Initiation,
                ProposedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), Status = CeremonyCodes.RequestStatus.UnderReview };
            var publication = new CandidatePublication { CeremonyRequest = ceremony, CeremonyRequestId = ceremony.Id,
                Organization = workshop, OrganizationId = workshop.Id, Person = person, PersonId = person.Id,
                PublishedFromUtc = DateTimeOffset.UtcNow.AddDays(i == 3 ? 2 : -3),
                PublishedUntilUtc = i == 2 ? DateTimeOffset.UtcNow.AddDays(-1) : null,
                RequiredDays = 20, RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
                Status = i == 4 ? "withdrawn" : CeremonyCodes.PublicationStatus.Published };
            var collection = new DocumentCollection { Code = $"PUB-{suffix}-{i}", Name = "Fotos privadas CI",
                Scope = DocumentManagementCodes.Scope.Organization, OrganizationId = workshop.Id };
            var document = new InstitutionalDocument { Collection = collection, OrganizationId = workshop.Id,
                Title = "PRIVATE-TITLE", DocumentType = "candidate_passport_photo",
                Classification = DocumentManagementCodes.Classification.Sensitive,
                AccessPolicy = DocumentManagementCodes.AccessPolicy.ManagementOnly,
                Status = DocumentManagementCodes.DocumentStatus.Active };
            var version = new DocumentVersion { Document = document, VersionNumber = 1,
                OriginalFileName = "PRIVATE-FILENAME.png", ContentType = "image/png", SizeBytes = PhotoBytes.Length,
                ObjectKey = $"PRIVATE-STORAGE/{Guid.NewGuid():N}",
                ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Available,
                Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(PhotoBytes)).ToLowerInvariant() };
            documents.AddRange(collection, document, version);
            await factory.ObjectStore.StoreAsync(version.ObjectKey, new MemoryStream(PhotoBytes), "image/png", ct);
            intake.CandidateIntakeProfiles.Add(new CandidateIntakeProfile { CeremonyRequestId = ceremony.Id,
                OrganizationId = workshop.Id, PersonId = person.Id, PaternalSurname = "PRIVATE-SURNAME",
                RutOrInstitutionalId = "PRIVATE-ID", InterviewSummary = "PRIVATE-INTERVIEW",
                InternalObservations = "PRIVATE-OBSERVATIONS", PhotoVersionId = i == 1 ? null : version.Id,
                SubmittedBySubject = "ci-synthetic", UpdatedBySubject = "ci-synthetic" });
            db.AddRange(person, ceremony, publication);
            result.Add(new(publication.Id, ceremony.Id, version.Id, $"{person.FirstNames} {person.LastNames}", i < 2, i != 1));
        }
        await db.SaveChangesAsync(ct);
        await documents.SaveChangesAsync(ct);
        await intake.SaveChangesAsync(ct);
        return new(workshop.Id, other.Id, workshop.Name, workshop.Number!, result.ToArray());
    }
}

internal sealed class CandidatePublicationTestFactory(string connection) : WebApplicationFactory<Program>
{
    public InMemoryDocumentObjectStore ObjectStore { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>(); services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.RemoveAll<CandidateIntakeDbContext>(); services.RemoveAll<DbContextOptions<CandidateIntakeDbContext>>();
            services.RemoveAll<DocumentManagementDbContext>(); services.RemoveAll<DbContextOptions<DocumentManagementDbContext>>();
            services.AddDbContext<PmgmDbContext>(o => o.UseNpgsql(connection));
            services.AddDbContext<CandidateIntakeDbContext>(o => o.UseNpgsql(connection));
            services.AddDbContext<DocumentManagementDbContext>(o => o.UseNpgsql(connection));
            services.RemoveAll<IDocumentObjectStore>(); services.AddSingleton<IDocumentObjectStore>(ObjectStore);
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = PublicationTestAuthentication.SchemeName;
                o.DefaultChallengeScheme = PublicationTestAuthentication.SchemeName;
                o.DefaultForbidScheme = PublicationTestAuthentication.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, PublicationTestAuthentication>(PublicationTestAuthentication.SchemeName, _ => { });
        });
    }
}

internal sealed class PublicationTestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CandidatePublication-CI";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Publication-Test-Organization"].FirstOrDefault(), out var organization))
            return Task.FromResult(AuthenticateResult.NoResult());
        var role = Request.Headers["X-Publication-Test-Role"].FirstOrDefault() ?? "member";
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "ci-publication-member"),
            new Claim(ClaimTypes.NameIdentifier, "ci-publication-member"),
            new Claim(InstitutionalClaims.Organization, organization.ToString()),
            new Claim(InstitutionalClaims.Role, role) }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
