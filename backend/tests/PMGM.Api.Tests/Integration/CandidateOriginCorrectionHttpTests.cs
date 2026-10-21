using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateOriginCorrectionHttpTests
{
    [Fact]
    public async Task Correction_requires_reason_and_persists_before_after_with_audit()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var workshop = new Organization {
            Name = "Taller C3 sintético " + Guid.NewGuid().ToString("N"), Type = "workshop"
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
        using var created = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new {
                organizationId = workshop.Id, firstNames = "Persona",
                lastNames = "C3 Sintética", rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "internet"
            }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = createdJson.GetProperty("ceremonyRequestId").GetGuid();
        var uri = "/api/insinuados/solicitudes/" + requestId + "/origen/corregir";

        using var missingReason = await client.PutAsJsonAsync(uri,
            new { originKind = "other", originOtherDescription = "Evento público", reason = "corta" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);

        const string reason = "Se verificó registro de participación en actividad institucional.";
        using var changed = await client.PutAsJsonAsync(uri,
            new { originKind = "other", originOtherDescription = "Evento público", reason }, ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var result = await changed.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("other", result.GetProperty("originKind").GetString());
        var correctionId = result.GetProperty("correctionId").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var corrected = await db.CeremonyRequests.AsNoTracking()
                .SingleAsync(x => x.Id == requestId, ct);
            Assert.Equal("other", corrected.CandidateOriginKind);
            Assert.Equal("Evento público", corrected.CandidateOriginOtherDescription);
            var history = await db.CandidateOriginCorrections.AsNoTracking()
                .SingleAsync(x => x.Id == correctionId, ct);
            Assert.Equal("internet", history.PreviousKind);
            Assert.Equal("other", history.NewKind);
            Assert.Null(history.PreviousRecommenderMemberId);
            Assert.Equal(reason, history.Reason);
            Assert.False(string.IsNullOrWhiteSpace(history.ChangedBySubject));
            var audit = await db.AuditEvents.AsNoTracking()
                .SingleAsync(x => x.EntityId == requestId.ToString("D") &&
                    x.Action == "candidate.origin.corrected", ct);
            Assert.DoesNotContain(reason, audit.MetadataJson ?? "");
        }

        using var replay = await client.PutAsJsonAsync(uri,
            new { originKind = "other", originOtherDescription = "Evento público", reason }, ct);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(1, await verifyDb.CandidateOriginCorrections
            .CountAsync(x => x.CeremonyRequestId == requestId, ct));
    }

    [Fact]
    public async Task Cannot_set_historical_interviewer_as_recommender()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var workshop = new Organization {
            Name = "Taller C3 bloqueo " + Guid.NewGuid().ToString("N"), Type = "workshop"
        };
        var person = new Person { FirstNames = "Hermano", LastNames = "Designado" };
        var member = new Member { Person = person, PersonId = person.Id, CurrentDegree = "master" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(workshop, member);
            await db.SaveChangesAsync(ct);
        }
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.Id.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
        using var created = await client.PostAsJsonAsync(
            "/api/insinuados/taller/solicitudes",
            new {
                organizationId = workshop.Id, firstNames = "Persona",
                lastNames = "Con entrevistador", rutOrInstitutionalId = Guid.NewGuid().ToString("N")[..12],
                originKind = "internet"
            }, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var response = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = response.GetProperty("ceremonyRequestId").GetGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            db.CandidateInterviewAssignments.Add(new CandidateInterviewAssignment {
                CeremonyRequestId = requestId, OrganizationId = workshop.Id,
                InterviewerMemberId = member.Id, Position = 1,
                CouncilBody = "administration_council",
                CouncilDecisionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CouncilMinuteReference = "ACTA-C3-SINTETICA",
                Status = "replaced", AssignedBySubject = "qa-c3"
            });
            await db.SaveChangesAsync(ct);
        }

        using var denied = await client.PutAsJsonAsync(
            "/api/insinuados/solicitudes/" + requestId + "/origen/corregir",
            new {
                originKind = "brother", recommenderMemberId = member.Id,
                reason = "Corrección solicitada después de designar a un entrevistador."
            }, ct);
        // Puede rechazar por identidad no acreditada (400) o conflicto histórico (409),
        // pero nunca crear la corrección ni cambiar el expediente.
        Assert.Contains(denied.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict });
        await using var verify = factory.Services.CreateAsyncScope();
        var dbVerify = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var ceremony = await dbVerify.CeremonyRequests.AsNoTracking().SingleAsync(x => x.Id == requestId, ct);
        Assert.Equal("internet", ceremony.CandidateOriginKind);
        Assert.Equal(0, await dbVerify.CandidateOriginCorrections.CountAsync(
            x => x.CeremonyRequestId == requestId, ct));
    }
}
