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
public sealed class CandidateInterviewAssignmentHttpTests
{
    [Fact]
    public async Task OnlyVenerableCanDesignateAndOtherWorkshopOrUnlinkedMastersFailClosed()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var origin = new Organization { Name = "Taller CI entrevistas", Type = "workshop" };
        var outsider = new Organization { Name = "Otro Taller CI entrevistas", Type = "workshop" };
        var candidate = new Person { FirstNames = "Insinuado", LastNames = "CI" };
        var request = new CeremonyRequest
        {
            Organization = origin, OrganizationId = origin.Id,
            CandidatePerson = candidate, CandidatePersonId = candidate.Id,
            CeremonyType = CeremonyCodes.Type.Initiation,
            RequiresFormalInterviewAssignments = true,
            Status = CeremonyCodes.RequestStatus.UnderReview
        };
        var publication = new CandidatePublication
        {
            CeremonyRequest = request, CeremonyRequestId = request.Id,
            Organization = origin, OrganizationId = origin.Id,
            Person = candidate, PersonId = candidate.Id,
            PublishedFromUtc = DateTimeOffset.UtcNow.AddDays(-1),
            RequiredDays = 20,
            RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
            Status = CeremonyCodes.PublicationStatus.Published
        };
        var deliberation = new CeremonyValidation
        {
            CeremonyRequest = request, CeremonyRequestId = request.Id,
            ValidationType = CeremonyCodes.ValidationType.CandidateInitialDeliberation,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            SourceReference = "ACTA-CI-UNANIME"
        };
        var people = Enumerable.Range(0, 4).Select(i =>
            new Person { FirstNames = "Maestro", LastNames = $"CI {i}" }).ToArray();
        var members = people.Select(p => new Member
        {
            Person = p, PersonId = p.Id, CurrentDegree = "master"
        }).ToArray();
        var memberships = members.Select((m, i) => new Membership
        {
            Member = m, MemberId = m.Id, Organization = i == 3 ? outsider : origin,
            OrganizationId = i == 3 ? outsider.Id : origin.Id,
            MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
        }).ToArray();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(origin, outsider, candidate, request, publication, deliberation);
            db.AddRange(members);
            db.AddRange(memberships);
            await db.SaveChangesAsync(ct);
        }
        try
        {
            var route = $"/api/insinuados/solicitudes/{request.Id}";
            client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", origin.Id.ToString());
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await client.GetAsync($"{route}/maestros-entrevistadores", ct)).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerVenerable);
            var lookup = await client.GetAsync($"{route}/maestros-entrevistadores", ct);
            Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
            Assert.True(lookup.Headers.CacheControl?.NoStore);
            var json = await lookup.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var returned = json.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("id").GetGuid()).ToHashSet();
            Assert.Equal(3, returned.Count);
            Assert.DoesNotContain(members[3].Id, returned);
            var body = new
            {
                interviewerMemberIds = new[] { members[0].Id, members[1].Id, members[3].Id },
                councilBody = "administration_council",
                councilDecisionDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                councilMinuteReference = "ACTA-CI-CONSEJO-123",
                scheduledDates = new DateOnly?[] { null, null, null }
            };
            var outsiderAttempt = await client.PostAsJsonAsync(
                $"{route}/entrevistadores-designados", body, ct);
            Assert.Equal(HttpStatusCode.Conflict, outsiderAttempt.StatusCode);
            var validExceptAccounts = await client.PostAsJsonAsync(
                $"{route}/entrevistadores-designados", new
                {
                    interviewerMemberIds = members.Take(3).Select(x => x.Id).ToArray(),
                    body.councilBody, body.councilDecisionDate,
                    body.councilMinuteReference, body.scheduledDates
                }, ct);
            Assert.Equal(HttpStatusCode.Conflict, validExceptAccounts.StatusCode);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.False(await db.CandidateInterviewAssignments.AnyAsync(
                x => x.CeremonyRequestId == request.Id, ct));
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.CandidateInterviewAssignments.Where(x => x.CeremonyRequestId == request.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CandidatePublications.Where(x => x.CeremonyRequestId == request.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CeremonyValidations.Where(x => x.CeremonyRequestId == request.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CeremonyRequests.Where(x => x.Id == request.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            var ids = members.Select(x => x.Id).ToArray();
            await db.Memberships.Where(x => ids.Contains(x.MemberId))
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Members.Where(x => ids.Contains(x.Id))
                .ExecuteDeleteAsync(CancellationToken.None);
            var personIds = people.Select(x => x.Id).Append(candidate.Id).ToArray();
            await db.People.Where(x => personIds.Contains(x.Id))
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Organizations.Where(x => x.Id == origin.Id || x.Id == outsider.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
    }
}
