using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateOriginPersistenceHttpTests
{
    [Fact]
    public async Task Secretary_creation_persists_accredited_origin_and_blocks_recommender_interview()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();

        var workshop = new Organization {
            Name = "Taller origen sintético " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var people = Enumerable.Range(0, 3).Select(i => new Person {
            FirstNames = "Maestro", LastNames = "Prueba " + i
        }).ToArray();
        var masters = people.Select(x => new Member {
            Person = x, PersonId = x.Id, CurrentDegree = "master"
        }).ToArray();
        var memberships = masters.Select(x => new Membership {
            Member = x, MemberId = x.Id, Organization = workshop,
            OrganizationId = workshop.Id,
            MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        }).ToArray();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Add(workshop);
            db.AddRange(masters);
            db.AddRange(memberships);
            await db.SaveChangesAsync(ct);
        }

        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.Id.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);

        var candidateRut = Guid.NewGuid().ToString("N")[..12];
        var creation = new {
            organizationId = workshop.Id,
            firstNames = "Persona", lastNames = "Candidata",
            rutOrInstitutionalId = candidateRut,
            originKind = "brother", recommenderMemberId = masters[0].Id
        };
        using var created = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes", creation, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = createdBody.GetProperty("ceremonyRequestId").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var request = await db.CeremonyRequests
                .Include(x => x.CandidatePerson)
                .SingleAsync(x => x.Id == requestId, ct);
            Assert.Equal("brother", request.CandidateOriginKind);
            Assert.Equal(masters[0].Id, request.CandidateRecommenderMemberId);
            Assert.Null(request.CandidateOriginOtherDescription);

            db.CandidatePublications.Add(new CandidatePublication {
                CeremonyRequestId = request.Id, CeremonyRequest = request,
                OrganizationId = workshop.Id, Organization = workshop,
                PersonId = request.CandidatePersonId!.Value,
                Person = request.CandidatePerson!,
                PublishedFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
                RequiredDays = 20,
                RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
                Status = CeremonyCodes.PublicationStatus.Published
            });
            db.CeremonyValidations.Add(new CeremonyValidation {
                CeremonyRequestId = request.Id, CeremonyRequest = request,
                ValidationType = CeremonyCodes.ValidationType.CandidateInitialDeliberation,
                Status = CeremonyCodes.ValidationStatus.Approved,
                AsOfDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                SourceReference = "ACTA-SINTETICA-ORIGEN"
            });
            await db.SaveChangesAsync(ct);
        }

        client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerVenerable);
        var route = "/api/insinuados/solicitudes/" + requestId;
        using var lookup = await client.GetAsync(route + "/maestros-entrevistadores", ct);
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var eligible = await lookup.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var ids = eligible.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.DoesNotContain(masters[0].Id, ids);
        Assert.Contains(masters[0].Id, eligible.GetProperty("excluded").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()));

        var decisionDay = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        using var blocked = await client.PostAsJsonAsync(route + "/entrevistadores-designados",
            new {
                interviewerMemberIds = masters.Select(x => x.Id).ToArray(),
                councilBody = "administration_council",
                councilDecisionDate = decisionDay,
                councilMinuteReference = "ACTA-SINTETICA-ORIGEN-2026",
                scheduledDates = new DateOnly?[] { null, null, null }
            }, ct);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var conflict = await blocked.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("CANDIDATE_RECOMMENDER_CANNOT_INTERVIEW",
            conflict.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_cross_workshop_recommender_fails_without_creating_case()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();

        var workshop = new Organization { Name = "Origen negativo " + Guid.NewGuid().ToString("N"), Type = "workshop" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Add(workshop);
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
        var fakeMemberId = Guid.NewGuid();
        using var result = await client.PostAsJsonAsync("/api/insinuados/taller/solicitudes",
            new {
                organizationId = workshop.Id, firstNames = "Persona", lastNames = "Sin fuente",
                rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "brother", recommenderMemberId = fakeMemberId
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var response = await result.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("ACTIVE_RECOMMENDER_NOT_VERIFIED", response.GetProperty("code").GetString());
    }
}
