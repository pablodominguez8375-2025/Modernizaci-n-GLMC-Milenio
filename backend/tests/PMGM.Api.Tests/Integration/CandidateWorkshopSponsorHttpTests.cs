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
    public async Task Declared_Chilean_RUT_requires_modulo_11_but_foreign_and_legacy_ids_remain_accepted()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();

        var workshop = new Organization {
            Name = "Taller identidad sintetica " + Guid.NewGuid().ToString("N"),
            Type = "workshop"
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Organizations.Add(workshop);
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.Id.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);

        // Identidad matemática K aleatoria: se minimiza colisión entre CI runs.
        string body;
        do { body = Random.Shared.Next(10_000_000, 99_999_999).ToString(); }
        while (!PMGM.Api.Modules.Core.ChileanRut.IsValid(body + "-K"));
        var presented = body[..2] + "." + body[2..5] + "." + body[5..] + "-k";

        using (var invalid = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes", new {
                organizationId = workshop.Id, firstNames = "Identidad",
                lastNames = "DV invalido", rutOrInstitutionalId = body + "-0",
                identityType = "chilean_rut"
            }, ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var error = await invalid.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal("INVALID_CHILEAN_RUT", error.GetProperty("code").GetString());
        }

        using (var unknown = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes", new {
                organizationId = workshop.Id, firstNames = "Identidad",
                lastNames = "Tipo desconocido", rutOrInstitutionalId = presented,
                identityType = "unknown"
            }, ct))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
            var error = await unknown.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal("CANDIDATE_IDENTITY_TYPE_INVALID", error.GetProperty("code").GetString());
        }

        using (var accepted = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes", new {
                organizationId = workshop.Id, firstNames = "Identidad",
                lastNames = "RUT chileno", rutOrInstitutionalId = presented,
                identityType = "chilean_rut"
            }, ct))
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);

        var foreignId = "EXT-" + Guid.NewGuid().ToString("N")[..10];
        using (var foreign = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes", new {
                organizationId = workshop.Id, firstNames = "Identidad",
                lastNames = "Documento extranjero", rutOrInstitutionalId = foreignId,
                identityType = "other"
            }, ct))
            Assert.Equal(HttpStatusCode.Created, foreign.StatusCode);

        var legacyId = "ID-" + Guid.NewGuid().ToString("N")[..11];
        using (var legacy = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes", new {
                organizationId = workshop.Id, firstNames = "Identidad",
                lastNames = "Compatibilidad", rutOrInstitutionalId = legacyId
            }, ct))
            Assert.Equal(HttpStatusCode.Created, legacy.StatusCode);

        await using var verify = factory.Services.CreateAsyncScope();
        var read = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.True(await read.People.AsNoTracking().AnyAsync(x => x.Rut == body + "-K", ct));
        Assert.True(await read.People.AsNoTracking().AnyAsync(x => x.Rut == foreignId.ToUpperInvariant(), ct));
        Assert.True(await read.People.AsNoTracking().AnyAsync(x => x.Rut == legacyId.ToUpperInvariant(), ct));
        Assert.False(await read.People.AsNoTracking().AnyAsync(x => x.Rut == body + "-0", ct));
    }

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
    [Fact]
    public async Task Sponsor_lookup_is_limited_to_active_workshop_members_and_secretariat()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();

        var host = new Organization {
            Name = "Taller patrocinio selector " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var other = new Organization {
            Name = "Taller hermano externo " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var localPerson = new Person { FirstNames = "Alicia", LastNames = "Patrocinadora" };
        var inactivePerson = new Person { FirstNames = "Berta", LastNames = "Inactiva" };
        var externalPerson = new Person { FirstNames = "Claudio", LastNames = "Externo" };
        var apprenticePerson = new Person { FirstNames = "Diana", LastNames = "Aprendiza" };
        var local = new Member { Person = localPerson, PersonId = localPerson.Id, CurrentDegree = "master" };
        var inactive = new Member { Person = inactivePerson, PersonId = inactivePerson.Id, CurrentDegree = "master" };
        var external = new Member { Person = externalPerson, PersonId = externalPerson.Id, CurrentDegree = "master" };
        var apprentice = new Member { Person = apprenticePerson, PersonId = apprenticePerson.Id, CurrentDegree = "apprentice" };
        var localMembership = new Membership {
            Member = local, MemberId = local.Id, Organization = host,
            OrganizationId = host.Id, MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        };
        var inactiveMembership = new Membership {
            Member = inactive, MemberId = inactive.Id, Organization = host,
            OrganizationId = host.Id, MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        };
        var externalMembership = new Membership {
            Member = external, MemberId = external.Id, Organization = other,
            OrganizationId = other.Id, MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        };
        var apprenticeMembership = new Membership {
            Member = apprentice, MemberId = apprentice.Id, Organization = host,
            OrganizationId = host.Id, MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        };
        var inactiveStatus = new InstitutionalStatusEvent {
            Member = inactive, MemberId = inactive.Id,
            Organization = host, OrganizationId = host.Id,
            EventType = MembershipCodes.InstitutionalStatus.Inactive,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(host, other, local, inactive, external, apprentice,
                localMembership, inactiveMembership, externalMembership, apprenticeMembership, inactiveStatus);
            await db.SaveChangesAsync(ct);
        }

        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", host.Id.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", "member");
        var uri = "/api/insinuados/taller/organizaciones/" + host.Id + "/patrocinadores";
        using var forbidden = await client.GetAsync(uri, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
        using var response = await client.GetAsync(uri, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var items = json.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items, x => x.GetProperty("id").GetGuid() == local.Id);
        Assert.DoesNotContain(items, x => x.GetProperty("id").GetGuid() == inactive.Id);
        Assert.DoesNotContain(items, x => x.GetProperty("id").GetGuid() == external.Id);
        Assert.DoesNotContain(items, x => x.GetProperty("id").GetGuid() == apprentice.Id);
        Assert.All(items, x => Assert.Equal(2, x.EnumerateObject().Count()));

        using var searched = await client.GetAsync(uri + "?q=Alicia", ct);
        Assert.Equal(HttpStatusCode.OK, searched.StatusCode);
        var filtered = await searched.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Single(filtered.GetProperty("items").EnumerateArray());

        using var wrongWorkshop = await client.GetAsync(
            "/api/insinuados/taller/organizaciones/" + other.Id + "/patrocinadores", ct);
        Assert.Equal(HttpStatusCode.Forbidden, wrongWorkshop.StatusCode);
        // La normativa exige patrocinante Maestro, no cualquier miembro local.
        using var rejectedApprentice = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new {
                organizationId = host.Id, firstNames = "Postulante",
                lastNames = "Sintético de prueba",
                rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "internet", sponsorMemberId = apprentice.Id
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedApprentice.StatusCode);
        var error = await rejectedApprentice.Content.ReadFromJsonAsync<JsonElement>(
            cancellationToken: ct);
        Assert.Equal("CANDIDATE_SPONSOR_NOT_ACTIVE_IN_WORKSHOP",
            error.GetProperty("code").GetString());
    }

}
