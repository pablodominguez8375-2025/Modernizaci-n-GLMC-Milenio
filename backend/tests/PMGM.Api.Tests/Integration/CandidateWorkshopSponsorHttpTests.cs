using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// #613 — el hermano origen puede ser externo, el patrocinador formal NO.
/// Datos sintéticos; requiere PMGM_TEST_POSTGRES en CI de integración.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateWorkshopSponsorHttpTests
{
    [Fact]
    public async Task Recommender_other_workshop_and_sponsor_local_are_distinct_and_valid()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();

        var host = new Organization {
            Name = "Taller receptor " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var remote = new Organization {
            Name = "Taller origen " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var originPerson = new Person { FirstNames = "Origen", LastNames = "Remoto" };
        var sponsorPerson = new Person { FirstNames = "Patrocinador", LastNames = "Local" };
        var recommender = new Member {
            Person = originPerson, PersonId = originPerson.Id, CurrentDegree = "master"
        };
        var sponsor = new Member {
            Person = sponsorPerson, PersonId = sponsorPerson.Id, CurrentDegree = "master"
        };
        var rm = new Membership {
            Member = recommender, MemberId = recommender.Id,
            Organization = remote, OrganizationId = remote.Id,
            MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        };
        var sm = new Membership {
            Member = sponsor, MemberId = sponsor.Id,
            Organization = host, OrganizationId = host.Id,
            MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(host, remote, recommender, sponsor, rm, sm);
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", host.Id.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);

        using var created = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new {
                organizationId = host.Id,
                firstNames = "Candidato", lastNames = "Sintético",
                rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "brother", recommenderMemberId = recommender.Id,
                sponsorMemberId = sponsor.Id
            }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = json.GetProperty("ceremonyRequestId").GetGuid();
        await using var verify = factory.Services.CreateAsyncScope();
        var core = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var row = await core.CeremonyRequests.AsNoTracking().SingleAsync(x => x.Id == requestId, ct);
        Assert.Equal(recommender.Id, row.CandidateRecommenderMemberId);
        Assert.Equal(sponsor.Id, row.CandidateSponsorMemberId);

        // El mismo hermano externo es admisible como origen pero no patrocinador.
        using var denied = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new {
                organizationId = host.Id,
                firstNames = "Candidato", lastNames = "Distinto",
                rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "internet", sponsorMemberId = recommender.Id
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        var error = await denied.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("CANDIDATE_SPONSOR_NOT_ACTIVE_IN_WORKSHOP",
            error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Internet_origin_can_start_as_draft_then_assign_verified_local_sponsor()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var host = new Organization {
            Name = "Taller patrocinio " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var remote = new Organization {
            Name = "Taller distinto " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var p = new Person { FirstNames = "Hermano", LastNames = "Activo" };
        var local = new Member { Person = p, PersonId = p.Id, CurrentDegree = "master" };
        var membership = new Membership {
            Member = local, MemberId = local.Id, Organization = host,
            OrganizationId = host.Id, MembershipType = "regular",
            Status = MembershipCodes.MembershipStatus.Active
        };
        var r = new Person { FirstNames = "Hermano", LastNames = "Externo" };
        var external = new Member { Person = r, PersonId = r.Id, CurrentDegree = "master" };
        var externalMembership = new Membership {
            Member = external, MemberId = external.Id, Organization = remote,
            OrganizationId = remote.Id, MembershipType = "regular",
            Status = MembershipCodes.MembershipStatus.Active
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(host, remote, local, external, membership, externalMembership);
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", host.Id.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
        using var created = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new {
                organizationId = host.Id, firstNames = "Candidato",
                lastNames = "Origen web", rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "internet"
            }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = json.GetProperty("ceremonyRequestId").GetGuid();
        // Origen registrado NO equivale a presentación institucional.
        // Conservar el borrador hasta asignar un patrocinador del Taller receptor.
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        using var blockedProfile = await client.PutAsJsonAsync(
            "/api/insinuados/solicitudes/" + requestId + "/ficha",
            new {
                firstNames = "Candidato", paternalSurname = "Origen web",
                presenters = new[] { "Texto heredado, NO evidencia de membresía" },
                insinuationDate = today
            }, ct);
        Assert.Equal(HttpStatusCode.Conflict, blockedProfile.StatusCode);
        var missingSponsor = await blockedProfile.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: ct);
        Assert.Equal("CANDIDATE_WORKSHOP_SPONSOR_REQUIRED",
            missingSponsor.GetProperty("code").GetString());
        var sponsorUrl = "/api/insinuados/taller/solicitudes/" + requestId + "/patrocinador";
        using var invalid = await client.PutAsJsonAsync(sponsorUrl, new {
            sponsorMemberId = external.Id
        }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var assigned = await client.PutAsJsonAsync(sponsorUrl, new {
            sponsorMemberId = local.Id
        }, ct);
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        await using var verify = factory.Services.CreateAsyncScope();
        var core = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var row = await core.CeremonyRequests.AsNoTracking().SingleAsync(x => x.Id == requestId, ct);
        Assert.Equal(local.Id, row.CandidateSponsorMemberId);
        Assert.Equal("internet", row.CandidateOriginKind);
        Assert.Null(row.CandidateRecommenderMemberId);
    }
}
