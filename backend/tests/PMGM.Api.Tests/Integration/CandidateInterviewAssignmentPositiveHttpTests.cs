using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.DocumentManagement;
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
        using var baseFactory = new CandidatePublicationTestFactory(connection);
        var scanner = new CleanDocumentMalwareScanner();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDocumentMalwareScanner>();
            services.AddSingleton<IDocumentMalwareScanner>(scanner);
        }));
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
        var ownIds = new Guid[3];
        var reportIds = new Guid[3];
        var payloads = new[] { PdfReport(), WordReport(), PdfReport() };
        try
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
                var notifications = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                await db.Database.MigrateAsync(ct);
                await notifications.Database.MigrateAsync(ct);
                var intake = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
                var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
                await intake.Database.MigrateAsync(ct);
                await documents.Database.MigrateAsync(ct);
                intake.CandidateIntakeProfiles.Add(new CandidateIntakeProfile
                {
                    CeremonyRequestId = ceremony.Id, OrganizationId = workshop.Id, PersonId = candidate.Id,
                    InsinuationDate = today.AddDays(-10), SubmittedBySubject = "ci-test", UpdatedBySubject = "ci-test"
                });
                await intake.SaveChangesAsync(ct);
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
                ownIds[index] = ownAssignment;
                Assert.Contains(ownAssignment, assignmentIds);
                var otherAssignment = assignmentIds.First(x => x != ownAssignment);
                var forbiddenOther = await client.PostAsync(
                    $"{route}/entrevistadores-designados/{otherAssignment}/aceptar", null, ct);
                Assert.Equal(HttpStatusCode.NotFound, forbiddenOther.StatusCode);
                using var beforeAcceptance = UploadRequest(route, ownAssignment, people[index], today, payloads[index], index);
                var deniedBeforeAcceptance = await client.SendAsync(beforeAcceptance, ct);
                Assert.Equal(HttpStatusCode.Conflict, deniedBeforeAcceptance.StatusCode);
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

            // Actual upload and delivery under three distinct institutional identities.
            client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", "member");
            for (var index = 0; index < members.Length; index++)
            {
                client.DefaultRequestHeaders.Remove("X-Publication-Test-Subject");
                client.DefaultRequestHeaders.Add("X-Publication-Test-Subject", subjects[index]);
                var ownId = ownIds[index];
                var otherId = ownIds[(index + 1) % ownIds.Length];
                using var otherUpload = UploadRequest(route, otherId, people[index], today, payloads[index], index);
                var deniedUpload = await client.SendAsync(otherUpload, ct);
                Assert.Equal(HttpStatusCode.Forbidden, deniedUpload.StatusCode);
                using var falseAuthor = UploadRequest(route, ownId, people[(index + 1) % 3], today, payloads[index], index);
                var deniedFalseAuthor = await client.SendAsync(falseAuthor, ct);
                Assert.Equal(HttpStatusCode.BadRequest, deniedFalseAuthor.StatusCode);
                using var ownUpload = UploadRequest(route, ownId, people[index], today, payloads[index], index);
                var uploaded = await client.SendAsync(ownUpload, ct);
                Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
                var uploadedJson = await uploaded.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                reportIds[index] = uploadedJson.GetProperty("documentVersionId").GetGuid();
                var deliverOther = await client.PostAsJsonAsync(
                    $"{route}/entrevistadores-designados/{otherId}/entregar",
                    new { documentVersionId = reportIds[index] }, ct);
                Assert.Equal(HttpStatusCode.NotFound, deliverOther.StatusCode);
                if (index < 2)
                {
                    var delivered = await client.PostAsJsonAsync(
                        $"{route}/entrevistadores-designados/{ownId}/entregar",
                        new { documentVersionId = reportIds[index] }, ct);
                    Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
                }
            }
            Assert.Equal(3, scanner.ScanCount);
            // A third file alone must not count as the third Maestro's personal delivery.
            var evidence = people.Select((person, i) => new CandidateInterviewEvidenceRequest(
                today, $"{person.FirstNames} {person.LastNames}", $"Resumen privado CI {i}",
                "favorable", reportIds[i])).ToArray();
            var package = new InterviewPackageRequest(today, evidence, true, "CUESTIONARIO-CI",
                true, "AUTOBIOGRAFIA-CI");
            var memberPackage = await client.PostAsJsonAsync($"{route}/antecedentes", package, ct);
            Assert.Equal(HttpStatusCode.Forbidden, memberPackage.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
            var thirdDegreeRequest = new ThirdDegreeReviewRequest(today, 3, 3, 0, 0, true, "ACTA-TERCER-GRADO-CI");
            var blockedReview = await client.PostAsJsonAsync($"{route}/revision-tercer-grado", thirdDegreeRequest, ct);
            Assert.Equal(HttpStatusCode.Conflict, blockedReview.StatusCode);
            var incomplete = await client.PostAsJsonAsync($"{route}/antecedentes", package, ct);
            Assert.Equal(HttpStatusCode.Conflict, incomplete.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", "member");
            var lastDeliveryRoute = $"{route}/entrevistadores-designados/{ownIds[2]}/entregar";
            var lastDelivery = await client.PostAsJsonAsync(lastDeliveryRoute,
                new { documentVersionId = reportIds[2] }, ct);
            Assert.Equal(HttpStatusCode.OK, lastDelivery.StatusCode);
            var repeatedDelivery = await client.PostAsJsonAsync(lastDeliveryRoute,
                new { documentVersionId = reportIds[2] }, ct);
            Assert.Equal(HttpStatusCode.Conflict, repeatedDelivery.StatusCode);

            // An interviewer cannot download a colleague's private report or see its metadata.
            for (var index = 0; index < members.Length; index++)
            {
                client.DefaultRequestHeaders.Remove("X-Publication-Test-Subject");
                client.DefaultRequestHeaders.Add("X-Publication-Test-Subject", subjects[index]);
                var otherDownload = await client.GetAsync(
                    $"/api/documentos/versiones/{reportIds[(index + 1) % 3]}/contenido", ct);
                Assert.Equal(HttpStatusCode.Forbidden, otherDownload.StatusCode);
                var assignments = await client.GetAsync($"{route}/entrevistadores-designados", ct);
                Assert.Equal(HttpStatusCode.Forbidden, assignments.StatusCode);
            }
            client.DefaultRequestHeaders.Remove("X-Publication-Test-Role");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Role", InstitutionalRoles.TallerSecretaria);
            client.DefaultRequestHeaders.Remove("X-Publication-Test-Organization");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", Guid.NewGuid().ToString());
            var wrongWorkshop = await client.GetAsync($"/api/documentos/versiones/{reportIds[0]}/contenido", ct);
            Assert.Equal(HttpStatusCode.Forbidden, wrongWorkshop.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Publication-Test-Organization");
            client.DefaultRequestHeaders.Add("X-Publication-Test-Organization", workshop.Id.ToString());
            for (var index = 0; index < reportIds.Length; index++)
            {
                var download = await client.GetAsync($"/api/documentos/versiones/{reportIds[index]}/contenido", ct);
                Assert.Equal(HttpStatusCode.OK, download.StatusCode);
                Assert.Equal(payloads[index], await download.Content.ReadAsByteArrayAsync(ct));
            }
            var missingBackground = await client.PostAsJsonAsync($"{route}/antecedentes",
                package with { ConfidentialQuestionnaireAvailable = false, AutobiographyAvailable = false }, ct);
            Assert.Equal(HttpStatusCode.OK, missingBackground.StatusCode);
            var missingJson = await missingBackground.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.NotEqual(CeremonyCodes.ValidationStatus.Approved,
                missingJson.GetProperty("validationStatus").GetString());
            var stillBlockedReview = await client.PostAsJsonAsync($"{route}/revision-tercer-grado", thirdDegreeRequest, ct);
            Assert.Equal(HttpStatusCode.Conflict, stillBlockedReview.StatusCode);
            var complete = await client.PostAsJsonAsync($"{route}/antecedentes", package, ct);
            Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
            var completeJson = await complete.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal(CeremonyCodes.ValidationStatus.Approved,
                completeJson.GetProperty("validationStatus").GetString());
            Assert.Equal(3, completeJson.GetProperty("completedInterviews").GetInt32());
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
                var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
                var deliveredRows = await db.CandidateInterviewAssignments.AsNoTracking()
                    .Where(x => x.CeremonyRequestId == ceremony.Id).ToListAsync(ct);
                Assert.All(deliveredRows, row => Assert.Equal("completed", row.Status));
                Assert.All(deliveredRows, row => Assert.NotNull(row.CompletedAtUtc));
                Assert.Equal(3, await db.AuditEvents.CountAsync(x => x.OrganizationId == workshop.Id &&
                    x.Action == "candidate.interview.report.delivered", ct));
                var versions = await documents.DocumentVersions.AsNoTracking()
                    .Include(x => x.Document).Where(x => reportIds.Contains(x.Id)).ToListAsync(ct);
                Assert.Equal(3, versions.Count);
                Assert.All(versions, version =>
                {
                    Assert.Equal(DocumentManagementCodes.ProcessingStatus.Available, version.ProcessingStatus);
                    Assert.Equal(DocumentManagementCodes.Classification.Sensitive, version.Document.Classification);
                    Assert.Equal(DocumentManagementCodes.AccessPolicy.ManagementOnly, version.Document.AccessPolicy);
                    Assert.True(DocumentIntegrity.IsValidSha256(version.Sha256));
                    Assert.StartsWith("clamav:test-clean:", version.ScanReference, StringComparison.Ordinal);
                });
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
            var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            var intake = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
            await documents.DocumentVersions.Where(x => assignmentIds.Contains(x.DocumentId))
                .ExecuteDeleteAsync(CancellationToken.None);
            await documents.InstitutionalDocuments.Where(x => assignmentIds.Contains(x.Id))
                .ExecuteDeleteAsync(CancellationToken.None);
            await documents.DocumentCollections.Where(x => x.OrganizationId == workshop.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await intake.CandidateIntakeProfiles.Where(x => x.CeremonyRequestId == ceremony.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
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

    private static HttpRequestMessage UploadRequest(string route, Guid assignmentId, Person person,
        DateOnly today, byte[] bytes, int index)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{route}/entrevistas/{assignmentId}/contenido")
        { Content = new ByteArrayContent(bytes) };
        var isWord = index == 1;
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(isWord
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/pdf");
        request.Headers.Add("X-File-Name", $"informe-ci-{index}.{(isWord ? "docx" : "pdf")}");
        request.Headers.Add("X-Interviewer", Uri.EscapeDataString($"{person.FirstNames} {person.LastNames}"));
        request.Headers.Add("X-Interview-Summary", Uri.EscapeDataString($"Resumen privado CI {index}"));
        request.Headers.Add("X-Interview-Result", "favorable");
        request.Headers.Add("X-Interview-Date", today.ToString("yyyy-MM-dd"));
        return request;
    }

    private static byte[] PdfReport()
        => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
            "2 0 obj<</Type/Pages/Count 0/Kids[]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");

    private static byte[] WordReport()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open()))
                writer.Write("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
                writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Informe sintetico CI</w:t></w:r></w:p></w:body></w:document>");
        }
        return buffer.ToArray();
    }

}
