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

/// <summary>
/// #613 — los circuitos de publicación y solicitud de iniciación comprueban
/// el Maestro patrocinante al momento de cada operación, no solo al crear ficha.
/// Todos los datos de prueba son sintéticos.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateFormalSponsorGatesHttpTests
{
    private static DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);

    [Fact]
    public async Task Modern_cases_block_publication_without_eligible_workshop_master()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var seeds = await SeedAsync(factory, ct);
        Authenticate(client, seeds.WorkshopId, InstitutionalRoles.GranSecretaria, true);

        foreach (var requestId in new[] { seeds.MissingId })
        {
            using var response = await client.PostAsync(
                $"/api/ceremonias/solicitudes/{requestId}/aprobar-publicacion-insinuado", null, ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal("CANDIDATE_WORKSHOP_SPONSOR_REQUIRED",
                json.GetProperty("code").GetString());
        }

        foreach (var requestId in new[] { seeds.RemoteId, seeds.ApprenticeId, seeds.InactiveId })
        {
            using var response = await client.PostAsync(
                $"/api/ceremonias/solicitudes/{requestId}/publicacion-insinuado", null, ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal("CANDIDATE_SPONSOR_NOT_ELIGIBLE_IN_WORKSHOP",
                json.GetProperty("code").GetString());
        }

        // El patrocinante sí es válido: ahora se bloquea el requisito SIGUIENTE,
        // deliberación inicial. Esta distinción prueba que el gate se superó.
        foreach (var requestId in new[] { seeds.ValidId, seeds.LegacyId })
        {
            using var response = await client.PostAsync(
                $"/api/ceremonias/solicitudes/{requestId}/aprobar-publicacion-insinuado", null, ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync(ct);
            Assert.Contains("deliberación inicial", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CANDIDATE_WORKSHOP_SPONSOR_REQUIRED", text);
        }
        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.False(await db.CandidatePublications.AsNoTracking().AnyAsync(
            x => new[] { seeds.MissingId, seeds.RemoteId, seeds.ApprenticeId, seeds.InactiveId }
                .Contains(x.CeremonyRequestId), ct));
    }

    [Fact]
    public async Task Modern_cases_block_formal_submission_but_local_master_and_legacy_can_proceed()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var seeds = await SeedAsync(factory, ct);
        Authenticate(client, seeds.WorkshopId, InstitutionalRoles.TallerSecretaria);
        var today = Today;
        var request = new {
            submissionDate = today,
            proposedCeremonyDate = today.AddDays(20),
            venerableApproval = true,
            secretaryDisplayName = "Secretaria sintética",
            sourceReference = "ACTA-C3-SINTETICA"
        };

        foreach (var (requestId, expectedCode) in new[] {
            (seeds.MissingId, "CANDIDATE_WORKSHOP_SPONSOR_REQUIRED"),
            (seeds.RemoteId, "CANDIDATE_SPONSOR_NOT_ELIGIBLE_IN_WORKSHOP"),
            (seeds.ApprenticeId, "CANDIDATE_SPONSOR_NOT_ELIGIBLE_IN_WORKSHOP"),
            (seeds.InactiveId, "CANDIDATE_SPONSOR_NOT_ELIGIBLE_IN_WORKSHOP")
        })
        {
            using var blocked = await client.PostAsJsonAsync(
                $"/api/insinuados/solicitudes/{requestId}/solicitud-iniciacion", request, ct);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            var json = await blocked.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal(expectedCode, json.GetProperty("code").GetString());
        }

        foreach (var requestId in new[] { seeds.ValidId, seeds.LegacyId })
        {
            using var accepted = await client.PostAsJsonAsync(
                $"/api/insinuados/solicitudes/{requestId}/solicitud-iniciacion", request, ct);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var json = await accepted.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal("approved", json.GetProperty("validationStatus").GetString());
        }

        await using var checkScope = factory.Services.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var failed = new[] { seeds.MissingId, seeds.RemoteId, seeds.ApprenticeId, seeds.InactiveId };
        Assert.Equal(0, await db.CeremonyValidations.AsNoTracking().CountAsync(
            x => failed.Contains(x.CeremonyRequestId) &&
                 x.ValidationType == CeremonyCodes.ValidationType.CandidateCeremonySubmission, ct));
    }

    private static void Authenticate(HttpClient client, Guid workshop, string role, bool order = false)
    {
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Organization");
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Scope");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", role);
        if (order) client.DefaultRequestHeaders.Add("X-Publication-Test-Scope", "order");
    }

    private static async Task<Cases> SeedAsync(CandidatePublicationTestFactory factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        await db.Database.MigrateAsync(ct);
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var workshop = new Organization {
            Name = "Taller receptor patrocinio " + suffix, Type = "workshop"
        };
        var remoteWorkshop = new Organization {
            Name = "Taller remoto patrocinio " + suffix, Type = "workshop"
        };

        static Member NewMember(string name, string degree) => new() {
            Person = new Person { FirstNames = name, LastNames = "Sintético" },
            CurrentDegree = degree
        };
        var master = NewMember("MaestroPatrocinador", "master");
        var remote = NewMember("MaestroExterno", "master");
        var apprentice = NewMember("AprendizLocal", "apprentice");
        var inactive = NewMember("MaestroInactivo", "master");
        Membership Membership(Member m, Organization o) => new() {
            Member = m, Organization = o,
            MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        };
        var inactiveEvent = new InstitutionalStatusEvent {
            Member = inactive, Organization = workshop,
            EventType = MembershipCodes.InstitutionalStatus.Inactive,
            EffectiveDate = Today.AddDays(-1)
        };
        CeremonyRequest NewRequest(string label, string? origin, Guid? sponsor = null) => new() {
            Organization = workshop,
            CandidatePerson = new Person { FirstNames = "Insinuado", LastNames = label + suffix },
            CeremonyType = CeremonyCodes.Type.Initiation,
            CandidateOriginKind = origin,
            CandidateSponsorMemberId = sponsor,
            Status = CeremonyCodes.RequestStatus.UnderReview
        };
        var missing = NewRequest("SinPatrocinio", "internet");
        var remoteCase = NewRequest("PatrocinioExterno", "social_media", remote.Id);
        var apprenticeCase = NewRequest("PatrocinioAprendiz", "internet", apprentice.Id);
        var inactiveCase = NewRequest("PatrocinioInactivo", "other", inactive.Id);
        // La fuente 'other' requiere descripción por el CHECK de la base.
        inactiveCase.CandidateOriginOtherDescription = "Evento abierto";
        var valid = NewRequest("PatrocinioMaestro", "internet", master.Id);
        var legacy = NewRequest("ExpedienteLegacy", null);
        var requests = new[] { missing, remoteCase, apprenticeCase, inactiveCase, valid, legacy };

        db.AddRange(workshop, remoteWorkshop, master, remote, apprentice, inactive,
            Membership(master, workshop), Membership(remote, remoteWorkshop),
            Membership(apprentice, workshop), Membership(inactive, workshop), inactiveEvent);
        db.CeremonyRequests.AddRange(requests);
        foreach (var ceremony in requests)
            db.CeremonyValidations.Add(new CeremonyValidation {
                CeremonyRequest = ceremony,
                ValidationType = CeremonyCodes.ValidationType.CandidateFinalBallot,
                Status = CeremonyCodes.ValidationStatus.Approved,
                AsOfDate = Today
            });
        await db.SaveChangesAsync(ct);
        return new Cases(workshop.Id, missing.Id, remoteCase.Id, apprenticeCase.Id,
            inactiveCase.Id, valid.Id, legacy.Id);
    }

    private sealed record Cases(Guid WorkshopId, Guid MissingId, Guid RemoteId,
        Guid ApprenticeId, Guid InactiveId, Guid ValidId, Guid LegacyId);
}
