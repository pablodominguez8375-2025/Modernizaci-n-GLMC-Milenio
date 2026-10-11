using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidateRePresentationReviewHttpTests
{
    [Fact]
    public async Task OnlyOrderRegimenInteriorCanResolveWithReferenceAndDecisionHistoryIsAppendOnly()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var workshop = new Organization { Name = "Taller QA RI de reinsinuaciones", Type = "workshop" };
        var person = new Person { FirstNames = "Persona", LastNames = "Demo RI" };
        var ceremony = new CeremonyRequest
        {
            Organization = workshop, OrganizationId = workshop.Id,
            CandidatePerson = person, CandidatePersonId = person.Id,
            CeremonyType = CeremonyCodes.Type.Initiation, Status = CeremonyCodes.RequestStatus.Draft
        };
        var historicalRequest = new CeremonyRequest
        {
            Organization = workshop, OrganizationId = workshop.Id,
            CandidatePerson = person, CandidatePersonId = person.Id,
            CeremonyType = CeremonyCodes.Type.Initiation, Status = CeremonyCodes.RequestStatus.Rejected
        };
        var historicalRejection = new CeremonyValidation
        {
            CeremonyRequest = historicalRequest, CeremonyRequestId = historicalRequest.Id,
            ValidationType = CeremonyCodes.ValidationType.CandidateFinalBallot,
            Status = CeremonyCodes.ValidationStatus.Rejected,
            AsOfDate = today.AddYears(-1), SourceReference = "ACTA-RECHAZO-PREVIO"
        };
        var original = new CeremonyValidation
        {
            CeremonyRequest = ceremony, CeremonyRequestId = ceremony.Id,
            ValidationType = CeremonyCodes.ValidationType.CandidateRepresentationReview,
            Status = CeremonyCodes.ValidationStatus.Observed,
            AsOfDate = today, SourceReference = "ACTA-SUBSANACION-DEL-TALLER",
            Notes = "Conserva antecedente histórico y solicita revisión."
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(workshop, person, ceremony, original, historicalRequest, historicalRejection);
            db.AuditEvents.Add(new AuditEvent
            {
                Action = "candidate.intake.created", EntityType = nameof(CeremonyRequest),
                EntityId = ceremony.Id.ToString(), OrganizationId = workshop.Id,
                ActorSubject = "creator-secretary", Result = "success",
                CorrelationId = Guid.NewGuid().ToString()
            });
            await db.SaveChangesAsync(ct);
        }

        var route = $"/api/insinuados/regimen-interior/reinsinuaciones/{ceremony.Id}/resolver";
        client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Publication-Test-Scope", "order");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Subject", "creator-secretary");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
        var forbidden = await client.PostAsJsonAsync(route,
            new { status = "approved", sourceReference = "RI-01" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.RegimenInterior);
        var selfApproval = await client.PostAsJsonAsync(route,
            new { status = "approved", sourceReference = "RI-01" }, ct);
        Assert.Equal(HttpStatusCode.Forbidden, selfApproval.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Publication-Test-Subject");
        client.DefaultRequestHeaders.Add("X-Publication-Test-Subject", "order-ri-reviewer");
        var missingReference = await client.PostAsJsonAsync(route,
            new { status = "approved", sourceReference = "" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingReference.StatusCode);
        var exception = await client.PostAsJsonAsync(route,
            new { status = "exception_approved", sourceReference = "RI-EX" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        var queue = await client.GetAsync("/api/insinuados/regimen-interior/reinsinuaciones", ct);
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        var queueJson = await queue.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Contains(queueJson.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("requestId").GetGuid() == ceremony.Id);

        var approve = await client.PostAsJsonAsync(route,
            new { status = "approved", sourceReference = "ACTA-RI-2026-001", notes = "Revisión documental conforme." }, ct);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approveJson = await approve.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("approved", approveJson.GetProperty("status").GetString());
        var replay = await client.PostAsJsonAsync(route,
            new { status = "approved", sourceReference = "ACTA-RI-2026-001", notes = "Revisión documental conforme." }, ct);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("replayed").GetBoolean());

        var observe = await client.PostAsJsonAsync(route,
            new { status = "observed", sourceReference = "ACTA-RI-2026-002", notes = "Nuevo antecedente requerido." }, ct);
        Assert.Equal(HttpStatusCode.OK, observe.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var history = await db.CeremonyValidations.AsNoTracking()
                .Where(x => x.CeremonyRequestId == ceremony.Id &&
                            x.ValidationType == CeremonyCodes.ValidationType.CandidateRepresentationReview)
                .OrderBy(x => x.RecordedAtUtc).ToListAsync(ct);
            Assert.Equal(3, history.Count);
            Assert.Contains(history, x => x.Id == original.Id && x.Status == CeremonyCodes.ValidationStatus.Observed);
            Assert.Equal("observed", history.Last().Status);
            Assert.Equal(2, await db.AuditEvents.AsNoTracking().CountAsync(x =>
                x.Action == "candidate.representation.interior_review_recorded" &&
                x.OrganizationId == workshop.Id, ct));
        }
    }
}
