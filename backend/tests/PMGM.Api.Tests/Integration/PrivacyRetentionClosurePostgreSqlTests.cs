using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Privacy;
using PMGM.Api.Modules.Privacy.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PrivacyRetentionClosurePostgreSqlTests
{
    [Fact]
    public async Task AnonymizationDoesNotClearAnOpenRequestAndRequiresFormalClosure()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;

        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        Guid requestId;
        Guid evaluationId;
        Guid personId;
        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);

            var person = new Person
            {
                FirstNames = "Titular Retencion",
                LastNames = "Sintetico"
            };
            personId = person.Id;
            var request = new DataSubjectRequest
            {
                Person = person, PersonId = person.Id,
                RequestType = PrivacyCodes.DataSubjectRight.Access,
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                Channel = "qa_fixture",
                IdentityVerified = true,
                Status = PrivacyCodes.Status.UnderReview,
                ResponsibleSubject = "qa-privacy-reviewer",
                Resolution = "Pendiente de resolución",
                Grounds = "Pendiente",
                EvidenceReference = "QA-PRIVACY-RETAIN"
            };
            requestId = request.Id;
            var policy = new DataRetentionPolicy
            {
                Code = $"QA-RET-{Guid.NewGuid():N}",
                Name = "QA política de privacidad",
                DataCategory = "privacy_requests",
                Purpose = "Prueba de ejecución restringida",
                LegalBasis = "QA synthetic only",
                RetentionDays = 30,
                ExpirationAction = PrivacyCodes.RetentionAction.Anonymize,
                AllowsLegalHold = true,
                Status = PrivacyCodes.Status.Active,
                EffectiveFrom = new DateOnly(2020, 1, 1)
            };
            var evaluation = new DataRetentionEvaluation
            {
                RetentionPolicy = policy,
                RetentionPolicyId = policy.Id,
                EntityType = nameof(DataSubjectRequest),
                EntityId = requestId.ToString("D"),
                AnchorDate = new DateOnly(2025, 1, 1),
                EvaluationDate = new DateOnly(2026, 1, 1),
                DueDate = new DateOnly(2025, 1, 31),
                RecommendedAction = PrivacyCodes.RetentionAction.Anonymize,
                BlockedByHold = false,
                Rationale = "QA synthetic",
                Status = RetentionEvaluationStatuses.Ready
            };
            evaluationId = evaluation.Id;
            db.AddRange(person, request, policy, evaluation);
            await db.SaveChangesAsync(ct);
        }

        var url = $"/api/privacy/retention/evaluations/{evaluationId}/execute";
        async Task AssertBlockedAsync()
        {
            using var denied = await client.PostAsJsonAsync(url,
                new { expectedAction = PrivacyCodes.RetentionAction.Anonymize,
                    evidenceReference = "QA-RETENTION-CHECK" }, ct);
            Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
            using var body = JsonDocument.Parse(await denied.Content.ReadAsStringAsync(ct));
            Assert.Equal("DATA_SUBJECT_REQUEST_NOT_FORMALLY_CLOSED",
                body.RootElement.GetProperty("code").GetString());
            Assert.True(body.RootElement.GetProperty("requiresManualReview").GetBoolean());

            await using var state = factory.Services.CreateAsyncScope();
            var db = state.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var preserved = await db.DataSubjectRequests.AsNoTracking()
                .SingleAsync(x => x.Id == requestId, ct);
            Assert.Equal(personId, preserved.PersonId);
            Assert.Equal("qa-privacy-reviewer", preserved.ResponsibleSubject);
            Assert.Equal("Pendiente de resolución", preserved.Resolution);
            Assert.Equal("Pendiente", preserved.Grounds);
            Assert.Equal("QA-PRIVACY-RETAIN", preserved.EvidenceReference);
            var stillPending = await db.Set<DataRetentionEvaluation>().AsNoTracking()
                .SingleAsync(x => x.Id == evaluationId, ct);
            Assert.Equal(RetentionEvaluationStatuses.Ready, stillPending.Status);
            Assert.Null(stillPending.ExecutedAtUtc);
            Assert.Null(stillPending.ExecutedAction);
        }

        await AssertBlockedAsync();
        await using (var firstClosure = factory.Services.CreateAsyncScope())
        {
            var db = firstClosure.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var request = await db.DataSubjectRequests.SingleAsync(x => x.Id == requestId, ct);
            request.Status = PrivacyCodes.Status.Closed;
            // Estado cerrado sin acta de cierre no autoriza anonimizar.
            await db.SaveChangesAsync(ct);
        }
        await AssertBlockedAsync();

        await using (var formalClosure = factory.Services.CreateAsyncScope())
        {
            var db = formalClosure.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var request = await db.DataSubjectRequests.SingleAsync(x => x.Id == requestId, ct);
            request.ClosedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        using var approved = await client.PostAsJsonAsync(url,
            new { expectedAction = PrivacyCodes.RetentionAction.Anonymize,
                evidenceReference = "QA-RETENTION-CHECK" }, ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        await using (var finalScope = factory.Services.CreateAsyncScope())
        {
            var db = finalScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var cleared = await db.DataSubjectRequests.AsNoTracking()
                .SingleAsync(x => x.Id == requestId, ct);
            Assert.Null(cleared.PersonId);
            Assert.Null(cleared.ResponsibleSubject);
            Assert.Null(cleared.Resolution);
            Assert.Null(cleared.Grounds);
            Assert.Null(cleared.EvidenceReference);
            var executed = await db.Set<DataRetentionEvaluation>().AsNoTracking()
                .SingleAsync(x => x.Id == evaluationId, ct);
            Assert.Equal(RetentionExecutionStatuses.Executed, executed.Status);
            Assert.Equal(PrivacyCodes.RetentionAction.Anonymize, executed.ExecutedAction);
        }
        using var replay = await client.PostAsJsonAsync(url,
            new { expectedAction = PrivacyCodes.RetentionAction.Anonymize,
                evidenceReference = "QA-RETENTION-CHECK" }, ct);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }
}
