using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.SecretariatOperations;
using PMGM.Api.Modules.SecretariatOperations.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class GrandSecretariatTenidaPrivacyTests
{
    [Fact]
    public async Task GranSecretaria_projection_exposes_only_basic_tenida_and_extract_reference()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        Guid meetingId;
        Guid recordId;
        Guid extractVersionId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var lodgeDb = scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Privacidad GS CI {Guid.NewGuid():N}",
                Number = "GS-PRIV",
                Type = "workshop"
            };
            db.Organizations.Add(organization);

            var meeting = new LodgeMeeting
            {
                OrganizationId = organization.Id,
                MeetingDate = new DateOnly(2026, 9, 18),
                MeetingType = LodgeManagementCodes.MeetingType.Regular,
                Grade = LodgeManagementCodes.Grade.Master,
                CeremonyType = null,
                Modality = LodgeManagementCodes.MeetingModality.Virtual,
                VirtualAccessReference = "https://privado.invalid/no-debe-exponerse",
                LocationReference = null,
                Title = "Tenida privacidad CI",
                Status = LodgeManagementCodes.MeetingStatus.Held,
                ClosedAtUtc = DateTimeOffset.UtcNow
            };
            lodgeDb.LodgeMeetings.Add(meeting);

            var record = new LodgeSecretariatRecord
            {
                OrganizationId = organization.Id,
                RecordType = SecretariatOperationsCodes.RecordType.LodgeMeeting,
                SourceRecordId = meeting.Id,
                EventDate = meeting.MeetingDate,
                Title = meeting.Title,
                WorkPaperDocumentVersionId = Guid.NewGuid(),
                WorkPaperAuthorMemberId = Guid.NewGuid(),
                ExtractDocumentVersionId = extractVersionId,
                FullMinuteDocumentVersionId = Guid.NewGuid(),
                Status = SecretariatOperationsCodes.SubmissionStatus.Submitted,
                CreatedBySubject = "secretaria-ci",
                SubmittedBySubject = "secretaria-ci",
                SubmittedAtUtc = DateTimeOffset.UtcNow
            };
            db.LodgeSecretariatRecords.Add(record);

            var privateMeeting = new LodgeAdministrativeMeeting
            {
                OrganizationId = organization.Id,
                MeetingDate = new DateOnly(2026, 9, 17),
                Title = "Reunión privada CI",
                Purpose = "No debe salir en Gran Secretaría",
                Status = SecretariatOperationsCodes.AdministrativeMeetingStatus.Held,
                CreatedBySubject = "secretaria-ci",
                HeldAtUtc = DateTimeOffset.UtcNow
            };
            db.LodgeAdministrativeMeetings.Add(privateMeeting);
            db.LodgeSecretariatRecords.Add(new LodgeSecretariatRecord
            {
                OrganizationId = organization.Id,
                RecordType = SecretariatOperationsCodes.RecordType.AdministrativeMeeting,
                SourceRecordId = privateMeeting.Id,
                EventDate = privateMeeting.MeetingDate,
                Title = privateMeeting.Title,
                ExtractDocumentVersionId = Guid.NewGuid(),
                Status = SecretariatOperationsCodes.SubmissionStatus.Submitted,
                CreatedBySubject = "secretaria-ci",
                SubmittedBySubject = "secretaria-ci",
                SubmittedAtUtc = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
            await lodgeDb.SaveChangesAsync(cancellationToken);
            organizationId = organization.Id;
            meetingId = meeting.Id;
            recordId = record.Id;
        }

        var response = await client.GetAsync($"/api/gran-secretaria/tenidas?organizationId={organizationId}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(items);

        Assert.Equal(recordId, item.GetProperty("recordId").GetGuid());
        Assert.Equal(meetingId, item.GetProperty("meetingId").GetGuid());
        Assert.Equal(extractVersionId, item.GetProperty("extractDocumentVersionId").GetGuid());
        Assert.Equal("virtual", item.GetProperty("modality").GetString());

        Assert.False(item.TryGetProperty("virtualAccessReference", out _));
        Assert.False(item.TryGetProperty("locationReference", out _));
        Assert.False(item.TryGetProperty("workPaperDocumentVersionId", out _));
        Assert.False(item.TryGetProperty("workPaperAuthorMemberId", out _));
        Assert.False(item.TryGetProperty("fullMinuteDocumentVersionId", out _));
        Assert.False(item.TryGetProperty("attendance", out _));
        Assert.False(item.TryGetProperty("ballots", out _));

        Assert.DoesNotContain("Reunión privada CI", response.Content.ReadAsStringAsync(cancellationToken).Result);
    }
}
