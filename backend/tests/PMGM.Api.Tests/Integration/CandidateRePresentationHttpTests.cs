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
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateRePresentationHttpTests
{
    [Fact]
    public async Task BothCreationEndpointsRequireOneYearAndRemediationEvidenceAndPreservePriorHistory()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var organization = new Organization { Name = $"Taller CI Reinsinuación {Guid.NewGuid():N}", Type = "workshop" };
        var secretaryCandidate = PersonFixture("Secretaría");
        var genericCandidate = PersonFixture("Ceremonias");
        var tooRecentCandidate = PersonFixture("Reciente");

        var oldDate = today.AddYears(-1);
        var recentDate = today.AddMonths(-2);
        var historic = new[]
        {
            HistoricalRejection(organization, secretaryCandidate, oldDate),
            HistoricalRejection(organization, genericCandidate, oldDate),
            HistoricalRejection(organization, tooRecentCandidate, recentDate),
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(organization, secretaryCandidate, genericCandidate, tooRecentCandidate);
            foreach (var item in historic)
            {
                db.CeremonyRequests.Add(item.Request);
                db.CeremonyValidations.Add(item.Validation);
            }
            await db.SaveChangesAsync(ct);
        }

        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", organization.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);

        object SecretaryPayload(Person person, bool remedied, string? reference)
            => new { organizationId = organization.Id, firstNames = person.FirstNames,
                lastNames = person.LastNames, rutOrInstitutionalId = person.Rut,
                rejectionCausesRemedied = remedied, rePresentationSourceReference = reference };

        var missingEvidence = await client.PostAsJsonAsync("/api/insinuados/taller/solicitudes",
            SecretaryPayload(secretaryCandidate, false, null), ct);
        Assert.Equal(HttpStatusCode.Conflict, missingEvidence.StatusCode);
        var missingBody = await missingEvidence.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("representation.causes_remedied", missingBody.GetProperty("code").GetString());

        var recent = await client.PostAsJsonAsync("/api/insinuados/taller/solicitudes",
            SecretaryPayload(tooRecentCandidate, true, "ACTA-SUBSANACION"), ct);
        Assert.Equal(HttpStatusCode.Conflict, recent.StatusCode);
        var recentBody = await recent.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("representation.minimum_year", recentBody.GetProperty("code").GetString());

        var secretaryResponse = await client.PostAsJsonAsync("/api/insinuados/taller/solicitudes",
            SecretaryPayload(secretaryCandidate, true, "ACTA-SECRETARIA-001"), ct);
        Assert.Equal(HttpStatusCode.Created, secretaryResponse.StatusCode);
        var secretaryJson = await secretaryResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var secretaryNewId = secretaryJson.GetProperty("ceremonyRequestId").GetGuid();

        object CeremonyPayload(bool remedied, string? reference)
            => new { organizationId = organization.Id, ceremonyType = CeremonyCodes.Type.Initiation,
                memberId = (Guid?)null, candidatePersonId = genericCandidate.Id,
                proposedDate = (DateOnly?)null, notes = "Prueba de compuerta alterna",
                rejectionCausesRemedied = remedied, rePresentationSourceReference = reference };

        var genericWithoutEvidence = await client.PostAsJsonAsync("/api/ceremonias/solicitudes",
            CeremonyPayload(false, null), ct);
        Assert.Equal(HttpStatusCode.Conflict, genericWithoutEvidence.StatusCode);

        var genericWithEvidence = await client.PostAsJsonAsync("/api/ceremonias/solicitudes",
            CeremonyPayload(true, "ACTA-GENERICA-001"), ct);
        Assert.Equal(HttpStatusCode.Created, genericWithEvidence.StatusCode);
        var genericJson = await genericWithEvidence.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var genericNewId = genericJson.GetProperty("id").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var newIds = new[] { secretaryNewId, genericNewId };
            var evidence = await db.CeremonyValidations.AsNoTracking()
                .Where(x => newIds.Contains(x.CeremonyRequestId) &&
                            x.ValidationType == CeremonyCodes.ValidationType.CandidateRepresentationReview)
                .ToListAsync(ct);
            Assert.Equal(2, evidence.Count);
            Assert.All(evidence, x => Assert.Equal(CeremonyCodes.ValidationStatus.Observed, x.Status));
            Assert.Contains(evidence, x => x.CeremonyRequestId == secretaryNewId &&
                                          x.SourceReference == "ACTA-SECRETARIA-001");
            Assert.Contains(evidence, x => x.CeremonyRequestId == genericNewId &&
                                          x.SourceReference == "ACTA-GENERICA-001");
            var priorIds = historic.Select(x => x.Validation.Id).ToArray();
            Assert.Equal(3, await db.CeremonyValidations.AsNoTracking()
                .CountAsync(x => priorIds.Contains(x.Id) && x.Status == CeremonyCodes.ValidationStatus.Rejected, ct));
            Assert.True(await db.AuditEvents.AsNoTracking()
                .AnyAsync(x => x.Action == "candidate.representation.evidence_recorded" &&
                               x.OrganizationId == organization.Id, ct));
        }
    }

    private static Person PersonFixture(string suffix)
        => new() { FirstNames = "Persona", LastNames = $"Ficticia {suffix}",
            Rut = $"QA-{Guid.NewGuid():N}"[..15] };

    private static (CeremonyRequest Request, CeremonyValidation Validation) HistoricalRejection(
        Organization organization, Person person, DateOnly date)
    {
        var request = new CeremonyRequest
        {
            Organization = organization, OrganizationId = organization.Id,
            CandidatePerson = person, CandidatePersonId = person.Id,
            CeremonyType = CeremonyCodes.Type.Initiation,
            Status = CeremonyCodes.RequestStatus.Rejected,
            RequiresFormalInterviewAssignments = true
        };
        var validation = new CeremonyValidation
        {
            CeremonyRequest = request, CeremonyRequestId = request.Id,
            ValidationType = CeremonyCodes.ValidationType.CandidateFinalBallot,
            Status = CeremonyCodes.ValidationStatus.Rejected,
            AsOfDate = date,
            SourceReference = $"ACTA-RECHAZO-{date:yyyyMMdd}"
        };
        return (request, validation);
    }
}
