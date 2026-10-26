using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// #752: regresión del GET /votaciones (4 fallos HTTP 500 en QA).
/// Usa la misma secuencia de los dos primeros pasos del migrador BD2:
/// PmgmDbContext (crea tenidas), luego LodgeManagementDbContext
/// (crea votaciones y ProcedureNumber). Ninguna dependencia de glmdch.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class LodgeBallotMigrationHttpTests
{
    [Fact]
    public async Task Lodge_ballot_migrations_are_repeatable_and_get_reads_empty_and_existing_ballots()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid meetingId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var institutional = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await institutional.Database.MigrateAsync(cancellationToken);

            var lodge = scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
            var knownMigrations = (lodge.Database.GetMigrations()).ToArray();
            Assert.Contains("20260912140000_AddAnonymousLodgeBallots", knownMigrations);
            Assert.Contains("20260912141000_AddBallotProcedureNumber", knownMigrations);

            await lodge.Database.MigrateAsync(cancellationToken);
            await lodge.Database.MigrateAsync(cancellationToken); // repeatable, no DDL duplicate
            Assert.Empty(await lodge.Database.GetPendingMigrationsAsync(cancellationToken));

            var organization = new Organization
            {
                Name = "Taller CI votaciones",
                Number = $"CI-{Guid.NewGuid():N}",
                Type = "workshop"
            };
            institutional.Organizations.Add(organization);
            await institutional.SaveChangesAsync(cancellationToken);

            var meeting = new LodgeMeeting
            {
                OrganizationId = organization.Id,
                MeetingDate = new DateOnly(2026, 10, 26),
                MeetingType = LodgeManagementCodes.MeetingType.Regular,
                Grade = LodgeManagementCodes.Grade.Master,
                Status = LodgeManagementCodes.MeetingStatus.Scheduled
            };
            lodge.LodgeMeetings.Add(meeting);
            await lodge.SaveChangesAsync(cancellationToken);
            meetingId = meeting.Id;
        }

        var path = $"/api/gestion-logial/tenidas/{meetingId}/votaciones";
        using (var empty = await client.GetAsync(path, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
            Assert.Contains("no-store", empty.Headers.CacheControl?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            var json = await empty.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            Assert.Equal(0, json.GetProperty("total").GetInt32());
            Assert.Empty(json.GetProperty("items").EnumerateArray());
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var lodge = scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
            lodge.LodgeAnonymousBallots.Add(new LodgeAnonymousBallot
            {
                MeetingId = meetingId,
                Version = 1,
                BallotType = LodgeManagementCodes.BallotType.WhiteBlack,
                ProcedureNumber = 1,
                Subject = "Votación sintética CI",
                AttendeeCount = 3,
                EligibleCount = 3,
                PositiveCount = 2,
                NegativeCount = 1,
                Status = LodgeManagementCodes.BallotStatus.Closed,
                RecordedBySubject = "ci-votaciones"
            });
            await lodge.SaveChangesAsync(cancellationToken);
        }

        using (var persisted = await client.GetAsync(path, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, persisted.StatusCode);
            var json = await persisted.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            Assert.Equal(1, json.GetProperty("total").GetInt32());
            var entry = Assert.Single(json.GetProperty("items").EnumerateArray());
            Assert.Equal("Votación sintética CI", entry.GetProperty("subject").GetString());
            Assert.Equal(1, entry.GetProperty("procedureNumber").GetInt32());
            Assert.Equal(2, entry.GetProperty("positiveCount").GetInt32());
        }

        using var unauthorized = new HttpRequestMessage(HttpMethod.Get, path);
        unauthorized.Headers.Add("X-Test-Role", InstitutionalRoles.TallerTesoreria);
        using var denied = await client.SendAsync(unauthorized, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
