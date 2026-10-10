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
public sealed class CandidateInterviewAssignmentPositiveHttpTests
{
    [Fact]
    public async Task ThreeMastersWithRealTestIdentitiesReceivePrivateAssignmentsAndAcceptIndividually()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new CandidatePublicationTestFactory(connection);
        using var client = factory.CreateClient();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var workshop = new Organization { Name = "Taller CI maestros positivos", Type = "workshop" };
        var candidate = new Person { FirstNames = "Insinuado", LastNames = "Positivo CI" };
        var ceremony = new CeremonyRequest
        {
            Organization = workshop, OrganizationId = workshop.Id,
            CandidatePerson = candidate, CandidatePersonId = candidate.Id,
            CeremonyType = CeremonyCodes.Type.Initiation,
            RequiresFormalInterviewAssignments = true,
            Status = CeremonyCodes.RequestStatus.UnderReview
        };
        var publication = new CandidatePublication
        {
            CeremonyRequest = ceremony, CeremonyRequestId = ceremony.Id,
            Organization = workshop, OrganizationId = workshop.Id,
            Person = candidate, PersonId = candidate.Id,
            PublishedFromUtc = DateTimeOffset.UtcNow.AddDays(-3),
            RequiredDays = 20,
            RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays,
            Status = CeremonyCodes.PublicationStatus.Published
        };
        var deliberation = new CeremonyValidation
        {
            CeremonyRequest = ceremony, CeremonyRequestId = ceremony.Id,
            ValidationType = CeremonyCodes.ValidationType.CandidateInitialDeliberation,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = today.AddDays(-4),
            SourceReference = "ACTA-UNANIME-CI-427"
        };
        var people = Enumerable.Range(1, 3).Select(i => new Person
            { FirstNames = "Maestro", LastNames = $"Sintético {i}" }).ToArray();
        var members = people.Select(person => new Member
            { Person = person, PersonId = person.Id, CurrentDegree = "master" }).ToArray();
        var subjects = members.Select((_, i) => $"ci-assignment-427-{Guid.NewGuid():N}-{i}").ToArray();
        var assignmentIds = Array.Empty<Guid>();
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
                var notifications = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                await db.Database.MigrateAsync(ct);
                await notifications.Database.MigrateAsync(ct);
                db.AddRange(workshop, candidate, ceremony, publication, deliberation);
                db.AddRange(members);
                foreach (var member in members)
                {
                    db.Memberships.Add(new Membership
                    {
                        Member = member, MemberId = member.Id,
                        Organization = workshop, OrganizationId = workshop.Id,
                        MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active
                    });
                    db.DegreeEvents.Add(new DegreeEvent
                    {
                        Member = member, MemberId = member.Id,
                        Organization = workshop, OrganizationId = workshop.Id,
                        Degree = "3", EventType = MembershipCodes.DegreeEvent.Exaltation,
                        EffectiveDate = today.AddDays(-20)
                    });
                }
                await db.SaveChangesAsync(ct);
                for (var index = 0; index < members.Length; index++)
                {
                    var linkId = Guid.NewGuid();
                    var memberId = members[index].Id;
                    var subject = subjects[index];
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO core.member_identity_links
                            ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject", "RevokedAtUtc")
                        VALUES ({linkId}, {memberId}, {"urn:pmgm:unspecified-issuer"},
                            {subject}, {DateTimeOffset.UtcNow}, {"ci-test"}, NULL)
                        """, ct);
                }
            }
            client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.Id.ToString());
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerVenerable);
            var route = $"/api/insinuados/solicitudes/{ceremony.Id}";
            var post = await client.PostAsJsonAsync($"{route}/entrevistadores-designados", new
            {
                interviewerMemberIds = members.Select(m => m.Id).ToArray(),
                councilBody = "administration_council",
                councilDecisionDate = today.AddDays(-1),
                councilMinuteReference = "ACTA-CONSEJO-CI-427-OK",
                scheduledDates = new DateOnly?[] { null, null, null }
            }, ct);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            var result = await post.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            assignmentIds = result.GetProperty("assignmentIds").EnumerateArray()
                .Select(x => x.GetGuid()).ToArray();
            Assert.Equal(3, assignmentIds.Length);
            Assert.Empty(result.GetProperty("notificationsPending").EnumerateArray());

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var notifications = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                var messages = await notifications.NotificationMessages.AsNoTracking()
                    .Where(x => subjects.Contains(x.RecipientSubject)).ToListAsync(ct);
                Assert.Equal(3, messages.Count);
                Assert.All(messages, item => Assert.DoesNotContain(candidate.FirstNames,
                    item.Body, StringComparison.OrdinalIgnoreCase));
                Assert.All(messages, item => Assert.Equal("restricted", item.Classification));
            }

            client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", "member");
            for (var index = 0; index < members.Length; index++)
            {
                client.DefaultRequestHeaders.Remove("X-Publication-Test-Subject");
                client.DefaultRequestHeaders.Add("X-Publication-Test-Subject", subjects[index]);
                var mine = await client.GetAsync("/api/insinuados/entrevistas/mis-designaciones", ct);
                Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
                Assert.True(mine.Headers.CacheControl?.NoStore);
                var task = await mine.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                var only = Assert.Single(task.GetProperty("items").EnumerateArray().ToArray());
                var ownAssignment = only.GetProperty("id").GetGuid();
                Assert.Contains(ownAssignment, assignmentIds);
                var otherAssignment = assignmentIds.First(x => x != ownAssignment);
                var forbiddenOther = await client.PostAsync(
                    $"{route}/entrevistadores-designados/{otherAssignment}/aceptar", null, ct);
                Assert.Equal(HttpStatusCode.NotFound, forbiddenOther.StatusCode);
                var accept = await client.PostAsync(
                    $"{route}/entrevistadores-designados/{ownAssignment}/aceptar", null, ct);
                Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
                var acceptAgain = await client.PostAsync(
                    $"{route}/entrevistadores-designados/{ownAssignment}/aceptar", null, ct);
                Assert.Equal(HttpStatusCode.OK, acceptAgain.StatusCode);
            }

            var rescheduleRoute = $"{route}/entrevistadores-designados/{assignmentIds[0]}/reprogramar";
            var scheduleChange = new { scheduledDate = today.AddDays(7), reason = "Ajuste de agenda del entrevistador CI" };
            var forbiddenReschedule = await client.PatchAsJsonAsync(rescheduleRoute, scheduleChange, ct);
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenReschedule.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerVenerable);
            var rescheduled = await client.PatchAsJsonAsync(rescheduleRoute, scheduleChange, ct);
            Assert.Equal(HttpStatusCode.OK, rescheduled.StatusCode);
            var retrySchedule = await client.PatchAsJsonAsync(rescheduleRoute, scheduleChange, ct);
            Assert.Equal(HttpStatusCode.OK, retrySchedule.StatusCode);
            var retryResult = await retrySchedule.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.False(retryResult.GetProperty("changed").GetBoolean());
            Assert.False(retryResult.GetProperty("notificationPending").GetBoolean());

            // Returning to a previously notified date is a new change, not a retry.
            var movedAgain = await client.PatchAsJsonAsync(rescheduleRoute,
                new { scheduledDate = today.AddDays(8), reason = "Segundo ajuste de agenda de prueba" }, ct);
            Assert.Equal(HttpStatusCode.OK, movedAgain.StatusCode);
            var returnedToDate = await client.PatchAsJsonAsync(rescheduleRoute, scheduleChange, ct);
            Assert.Equal(HttpStatusCode.OK, returnedToDate.StatusCode);
            var retriedReturn = await client.PatchAsJsonAsync(rescheduleRoute, scheduleChange, ct);
            Assert.Equal(HttpStatusCode.OK, retriedReturn.StatusCode);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var notifications = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                var notices = await notifications.NotificationMessages.AsNoTracking()
                    .Where(x => subjects.Contains(x.RecipientSubject) &&
                        x.TypeCode == "candidate.interview.rescheduled").ToListAsync(ct);
                Assert.Equal(3, notices.Count);
                Assert.All(notices, notice => Assert.Equal("restricted", notice.Classification));
                Assert.All(notices, notice => Assert.DoesNotContain(candidate.FirstNames, notice.Body, StringComparison.OrdinalIgnoreCase));
                var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
                var rows = await db.CandidateInterviewAssignments.AsNoTracking()
                    .Where(x => x.CeremonyRequestId == ceremony.Id).ToListAsync(ct);
                Assert.Equal(3, rows.Count);
                Assert.All(rows, row => Assert.NotNull(row.AcceptedAtUtc));
                var changedRow = Assert.Single(rows, x => x.Id == assignmentIds[0]);
                Assert.Equal(today.AddDays(7), changedRow.ScheduledDate);
                Assert.All(rows.Where(x => x.Id != changedRow.Id), row => Assert.Null(row.ScheduledDate));
                Assert.All(notices, notice => Assert.Equal(subjects[Array.FindIndex(members, m => m.Id == changedRow.InterviewerMemberId)], notice.RecipientSubject));
                Assert.Equal(3, await db.AuditEvents.CountAsync(x =>
                    x.OrganizationId == workshop.Id && x.Action == "candidate.interview.schedule.changed", ct));
                Assert.Equal(3, await db.AuditEvents.CountAsync(x =>
                    x.OrganizationId == workshop.Id &&
                    x.Action == "candidate.interview.designation.accepted", ct));
            }
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            await notifications.NotificationMessages.Where(x => subjects.Contains(x.RecipientSubject))
                .ExecuteDeleteAsync(CancellationToken.None);
            foreach (var subject in subjects)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM core.member_identity_links WHERE \"Subject\" = {subject}",
                    CancellationToken.None);
            await db.AuditEvents.Where(x => x.OrganizationId == workshop.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CandidateInterviewAssignments.Where(x => x.CeremonyRequestId == ceremony.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CandidatePublications.Where(x => x.CeremonyRequestId == ceremony.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CeremonyValidations.Where(x => x.CeremonyRequestId == ceremony.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.CeremonyRequests.Where(x => x.Id == ceremony.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            var ids = members.Select(x => x.Id).ToArray();
            await db.DegreeEvents.Where(x => ids.Contains(x.MemberId))
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Memberships.Where(x => ids.Contains(x.MemberId))
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Members.Where(x => ids.Contains(x.Id))
                .ExecuteDeleteAsync(CancellationToken.None);
            var personIds = people.Select(x => x.Id).Append(candidate.Id).ToArray();
            await db.People.Where(x => personIds.Contains(x.Id))
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Organizations.Where(x => x.Id == workshop.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
        }
    }
}
