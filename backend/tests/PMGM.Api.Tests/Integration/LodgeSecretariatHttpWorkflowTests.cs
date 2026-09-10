using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LodgeSecretariatHttpWorkflowTests
{
    [Fact]
    public async Task Correspondence_tasks_and_meeting_agenda_are_scoped_persisted_and_audited()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Secretaría Logial CI {Guid.NewGuid():N}",
                Number = "SEC-CI",
                Type = "workshop"
            };
            var person = new Person { FirstNames = "Hermana", LastNames = "Secretaría CI" };
            var member = new Member
            {
                Person = person,
                PersonId = person.Id,
                InstitutionalNumber = $"SEC-{Guid.NewGuid():N}"
            };
            var membership = new Membership
            {
                Member = member,
                MemberId = member.Id,
                Organization = organization,
                OrganizationId = organization.Id,
                MembershipType = "regular",
                StartDate = new DateOnly(2026, 1, 1),
                Status = MembershipCodes.MembershipStatus.Active
            };
            db.AddRange(organization, person, member, membership);
            await db.SaveChangesAsync(cancellationToken);
            organizationId = organization.Id;
        }

        var meetingResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/tenidas",
            new
            {
                meetingDate = new DateOnly(2026, 9, 18),
                meetingType = LodgeManagementCodes.MeetingType.Regular,
                grade = LodgeManagementCodes.Grade.All,
                title = "Tenida para Secretaría CI"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, meetingResponse.StatusCode);
        var meetingJson = await meetingResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var meetingId = meetingJson.GetProperty("id").GetGuid();

        const string confidentialSubject = "Asunto reservado CI que no debe aparecer en auditoría";
        const string confidentialCounterparty = "Contraparte CI reservada";
        var correspondenceResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/secretaria/correspondencia",
            new
            {
                folio = "SEC-CI-001",
                direction = LodgeSecretariatCodes.CorrespondenceDirection.Incoming,
                correspondenceDate = new DateOnly(2026, 9, 10),
                subject = confidentialSubject,
                counterparty = confidentialCounterparty,
                channel = LodgeSecretariatCodes.Channel.Platform,
                externalReference = "REF-CI-001",
                notes = "Nota restringida CI"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, correspondenceResponse.StatusCode);
        var correspondenceJson = await correspondenceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var correspondenceId = correspondenceJson.GetProperty("id").GetGuid();
        Assert.Equal(LodgeSecretariatCodes.CorrespondenceStatus.Registered, correspondenceJson.GetProperty("status").GetString());

        var duplicateFolio = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/secretaria/correspondencia",
            new
            {
                folio = "SEC-CI-001",
                direction = LodgeSecretariatCodes.CorrespondenceDirection.Outgoing,
                correspondenceDate = new DateOnly(2026, 9, 10),
                subject = "Duplicado",
                counterparty = "CI",
                channel = LodgeSecretariatCodes.Channel.Email
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateFolio.StatusCode);

        var processCorrespondence = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/secretaria/correspondencia/{correspondenceId}/estado",
            new { status = LodgeSecretariatCodes.CorrespondenceStatus.Processed },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, processCorrespondence.StatusCode);

        var taskResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/secretaria/pendientes",
            new
            {
                title = "Preparar documentación CI",
                detail = "Detalle reservado de la tarea CI",
                dueDate = new DateOnly(2026, 9, 17),
                priority = LodgeSecretariatCodes.Priority.High,
                responsibleLabel = "Secretaría"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
        var taskJson = await taskResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var taskId = taskJson.GetProperty("id").GetGuid();

        var completeTask = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/secretaria/pendientes/{taskId}/estado",
            new { status = LodgeSecretariatCodes.TaskStatus.Done },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, completeTask.StatusCode);

        var agendaResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/tenidas/{meetingId}/tabla",
            new { title = "Cuenta de Secretaría CI", detail = "Detalle de tabla reservado CI" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, agendaResponse.StatusCode);
        var agendaJson = await agendaResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var agendaId = agendaJson.GetProperty("id").GetGuid();
        Assert.Equal(1, agendaJson.GetProperty("position").GetInt32());

        var resolveAgenda = await client.PostAsJsonAsync(
            $"/api/gestion-logial/tenidas/{meetingId}/tabla/{agendaId}/estado",
            new { status = LodgeSecretariatCodes.AgendaStatus.Addressed },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, resolveAgenda.StatusCode);

        var correspondenceList = await client.GetAsync($"/api/gestion-logial/talleres/{organizationId}/secretaria/correspondencia", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, correspondenceList.StatusCode);
        Assert.Equal("private, no-store", correspondenceList.Headers.CacheControl?.ToString());
        var listJson = await correspondenceList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, listJson.GetProperty("total").GetInt32());
        Assert.Equal(confidentialSubject, listJson.GetProperty("items")[0].GetProperty("subject").GetString());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var lodgeDb = verificationScope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
        Assert.Equal(1, await lodgeDb.LodgeCorrespondenceRecords.CountAsync(x => x.OrganizationId == organizationId, cancellationToken));
        Assert.Equal(1, await lodgeDb.LodgeSecretariatTasks.CountAsync(x => x.OrganizationId == organizationId && x.Status == LodgeSecretariatCodes.TaskStatus.Done, cancellationToken));
        Assert.Equal(1, await lodgeDb.LodgeMeetingAgendaItems.CountAsync(x => x.MeetingId == meetingId && x.Status == LodgeSecretariatCodes.AgendaStatus.Addressed, cancellationToken));

        var audits = await lodgeDb.AuditEvents.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && x.Action.StartsWith("lodge.secretariat."))
            .Select(x => new { x.Action, x.MetadataJson })
            .ToListAsync(cancellationToken);
        Assert.Equal(6, audits.Count);
        Assert.Contains(audits, x => x.Action == "lodge.secretariat.correspondence.created");
        Assert.Contains(audits, x => x.Action == "lodge.secretariat.task.status_changed");
        Assert.Contains(audits, x => x.Action == "lodge.secretariat.agenda.status_changed");
        Assert.All(audits, item =>
        {
            Assert.DoesNotContain(confidentialSubject, item.MetadataJson ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(confidentialCounterparty, item.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        });
    }
}
