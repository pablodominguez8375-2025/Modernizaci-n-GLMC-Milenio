using System.Net;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Privacy;
using PMGM.Api.Modules.Privacy.Entities;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PrivacyRequestReadAuditPostgreSqlTests
{
    [Fact]
    public async Task ListingDataSubjectRequestsPersistsReadAuditWithoutSubjectIdentifiers()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var correlationId = $"privacy-read-test-{Guid.NewGuid():N}";
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
        }

        var response = await client.GetAsync("/api/privacy/data-subject-requests", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(JsonValueKind.Array, responseBody.ValueKind);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var metadata = await verificationDb.AuditEvents.AsNoTracking()
            .Where(x => x.CorrelationId == correlationId)
            .Select(x => x.MetadataJson)
            .SingleAsync(cancellationToken);

        Assert.NotNull(metadata);
        using var document = JsonDocument.Parse(metadata);
        var root = document.RootElement;
        Assert.Equal(responseBody.GetArrayLength(), root.GetProperty("resultCount").GetInt32());
        Assert.False(root.TryGetProperty("PersonId", out _));
        Assert.False(root.TryGetProperty("MemberId", out _));
    }
    [Fact]
    public async Task SelfServiceOnlyCreatesAndListsAuthenticatedPersonsRequests()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs);
        using var client = factory.CreateClient();

        const string issuer = "urn:pmgm:unspecified-issuer";
        const string subject = "ci-http-admin";
        var ownPerson = new Person { FirstNames = "Titular", LastNames = "Prueba Privacidad" };
        var otherPerson = new Person { FirstNames = "Tercero", LastNames = "Prueba Privacidad" };
        var member = new Member { Person = ownPerson, PersonId = ownPerson.Id };
        var organization = new Organization { Name = "Taller Privacidad QA", Type = "workshop" };
        var degree = new DegreeEvent
        {
            Member = member, MemberId = member.Id,
            Organization = organization, OrganizationId = organization.Id,
            Degree = "3", EventType = MembershipCodes.DegreeEvent.Exaltation,
            EffectiveDate = new DateOnly(2022, 1, 10)
        };
        var foreign = new DataSubjectRequest
        {
            PersonId = otherPerson.Id, RequestType = PrivacyCodes.DataSubjectRight.Access,
            ReceivedAtUtc = DateTimeOffset.UtcNow, Channel = "manual",
            IdentityVerified = true, Status = PrivacyCodes.Status.UnderReview
        };

        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM core.member_identity_links WHERE \"Issuer\" = {issuer} AND \"Subject\" = {subject}", ct);
            db.AddRange(ownPerson, otherPerson, member, organization, degree, foreign);
            await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links
                    ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject", "RevokedAtUtc")
                VALUES ({Guid.NewGuid()}, {member.Id}, {issuer}, {subject}, {DateTimeOffset.UtcNow}, {subject}, NULL)
                """, ct);
        }

        using var invalid = await client.PostAsJsonAsync("/api/privacy/data-subject-requests/me",
            new { requestType = "invalid_right" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        // JSON fields from the browser cannot choose a different titular or
        // declare legal verification / resolution: the DTO only accepts right.
        using var created = await client.PostAsJsonAsync("/api/privacy/data-subject-requests/me",
            new { requestType = PrivacyCodes.DataSubjectRight.Access,
                personId = otherPerson.Id, identityVerified = true, status = "closed",
                dueDate = "2000-01-01" }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains("no-store", created.Headers.CacheControl?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = createdJson.GetProperty("id").GetGuid();
        Assert.Equal("under_review", createdJson.GetProperty("status").GetString());
        Assert.False(createdJson.TryGetProperty("personId", out _));
        Assert.False(createdJson.TryGetProperty("identityVerified", out _));

        using var own = await client.GetAsync("/api/privacy/data-subject-requests/me", ct);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Contains("no-store", own.Headers.CacheControl?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        var listing = await own.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var items = listing.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(requestId, items[0].GetProperty("id").GetGuid());
        Assert.False(items[0].TryGetProperty("personId", out _));
        Assert.False(items[0].TryGetProperty("responsibleSubject", out _));
        Assert.False(items[0].TryGetProperty("resolution", out _));
        Assert.False(items[0].TryGetProperty("evidenceReference", out _));

        await using (var check = factory.Services.CreateAsyncScope())
        {
            var db = check.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var stored = await db.DataSubjectRequests.AsNoTracking()
                .SingleAsync(x => x.Id == requestId, ct);
            Assert.Equal(ownPerson.Id, stored.PersonId);
            Assert.NotEqual(otherPerson.Id, stored.PersonId);
            Assert.False(stored.IdentityVerified);
            Assert.Equal("authenticated_portal", stored.Channel);
            Assert.Equal(PrivacyCodes.Status.UnderReview, stored.Status);
            Assert.NotEqual(foreign.Id, stored.Id);
            var audits = await db.AuditEvents.AsNoTracking()
                .Where(x => x.Action == "privacy.data_subject_request.self.created"
                    && x.EntityId == requestId.ToString())
                .Select(x => x.MetadataJson).ToListAsync(ct);
            Assert.Single(audits);
            Assert.DoesNotContain(ownPerson.Id.ToString(), audits[0] ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(otherPerson.Id.ToString(), audits[0] ?? "", StringComparison.OrdinalIgnoreCase);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE core.member_identity_links SET \"RevokedAtUtc\" = {DateTimeOffset.UtcNow} WHERE \"Issuer\" = {issuer} AND \"Subject\" = {subject}", ct);
        }

        using var revokedRead = await client.GetAsync("/api/privacy/data-subject-requests/me", ct);
        Assert.Equal(HttpStatusCode.NotFound, revokedRead.StatusCode);
        using var revokedWrite = await client.PostAsJsonAsync("/api/privacy/data-subject-requests/me",
            new { requestType = PrivacyCodes.DataSubjectRight.Access }, ct);
        Assert.Equal(HttpStatusCode.NotFound, revokedWrite.StatusCode);
    }

}
