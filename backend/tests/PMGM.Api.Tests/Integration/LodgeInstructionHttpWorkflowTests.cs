using System.Globalization;
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
public sealed class LodgeInstructionHttpWorkflowTests
{
    private const string TestIssuer = "urn:pmgm:unspecified-issuer";
    private const string TestSubject = "ci-lodge-admin";

    [Fact]
    public async Task Instruction_attendance_and_member_history_behave_like_class_attendance()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new LodgeManagementWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        Guid memberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM core.member_identity_links WHERE \"Issuer\" = {TestIssuer} AND \"Subject\" = {TestSubject}",
                cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Instrucción CI {Guid.NewGuid():N}",
                Number = $"I-{Guid.NewGuid():N}"[..10],
                Type = "workshop"
            };
            var person = new Person
            {
                FirstNames = "Hermano",
                LastNames = "Aprendiz CI",
                Email = $"aprendiz-{Guid.NewGuid():N}@example.test"
            };
            var member = new Member
            {
                Person = person,
                PersonId = person.Id,
                InstitutionalNumber = $"INS-{Guid.NewGuid():N}"[..20]
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
            var degreeEvent = new DegreeEvent
            {
                Member = member,
                MemberId = member.Id,
                Organization = organization,
                OrganizationId = organization.Id,
                Degree = "1",
                EventType = MembershipCodes.DegreeEvent.Initiation,
                EffectiveDate = new DateOnly(2026, 1, 15)
            };

            db.AddRange(organization, person, member, membership, degreeEvent);
            await db.SaveChangesAsync(cancellationToken);

            var linkId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links
                    ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject", "RevokedAtUtc")
                VALUES
                    ({linkId}, {member.Id}, {TestIssuer}, {TestSubject}, {createdAt}, {TestSubject}, NULL)
                """, cancellationToken);

            organizationId = organization.Id;
            memberId = member.Id;
        }

        client.DefaultRequestHeaders.Add("X-Test-Organization", organizationId.ToString());

        var wrongGrade = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/instrucciones",
            new { instructionDate = new DateOnly(2026, 9, 8), grade = LodgeManagementCodes.Grade.Master, topic = "Grado no asignado" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, wrongGrade.StatusCode);

        var filteredMembers = await client.GetAsync(
            $"/api/gestion-logial/talleres/{organizationId}/miembros/opciones?grade=apprentice&asOf=2026-09-08",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, filteredMembers.StatusCode);
        var filteredJson = await filteredMembers.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, filteredJson.GetProperty("total").GetInt32());
        Assert.Equal(memberId, filteredJson.GetProperty("items")[0].GetProperty("id").GetGuid());

        const string topic = "Símbolos y deberes del Aprendiz";
        var createResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/instrucciones",
            new
            {
                instructionDate = new DateOnly(2026, 9, 8),
                grade = LodgeManagementCodes.Grade.Apprentice,
                topic,
                instructorMemberId = (Guid?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var instructionId = createJson.GetProperty("id").GetGuid();
        Assert.Equal(LodgeManagementCodes.InstructionOffice.SecondWarden, createJson.GetProperty("responsibleOffice").GetString());
        Assert.Equal(LodgeManagementCodes.InstructionStatus.Scheduled, createJson.GetProperty("status").GetString());
        Assert.Equal(topic, createJson.GetProperty("topic").GetString());

        var prematureAttendance = await client.PostAsJsonAsync(
            $"/api/gestion-logial/instrucciones/{instructionId}/asistencia",
            new { items = new[] { new { memberId, status = LodgeManagementCodes.InstructionAttendanceStatus.Present } } },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, prematureAttendance.StatusCode);

        var completeResponse = await client.PostAsync(
            $"/api/gestion-logial/instrucciones/{instructionId}/realizar", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var absentResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/instrucciones/{instructionId}/asistencia",
            new
            {
                items = new[]
                {
                    new { memberId, status = LodgeManagementCodes.InstructionAttendanceStatus.Absent }
                }
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, absentResponse.StatusCode);

        var initialHistory = await client.GetAsync(
            $"/api/gestion-logial/miembros/{memberId}/instrucciones",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, initialHistory.StatusCode);
        var initialJson = await initialHistory.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, initialJson.GetProperty("total").GetInt32());
        var initialItem = initialJson.GetProperty("items")[0];
        Assert.Equal(topic, initialItem.GetProperty("topic").GetString());
        Assert.False(initialItem.GetProperty("attended").GetBoolean());
        Assert.Equal(LodgeManagementCodes.InstructionAttendanceStatus.Absent, initialItem.GetProperty("status").GetString());

        var correctionResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/instrucciones/{instructionId}/asistencia",
            new
            {
                items = new[]
                {
                    new { memberId, status = LodgeManagementCodes.InstructionAttendanceStatus.Present }
                }
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, correctionResponse.StatusCode);

        var correctedHistory = await client.GetAsync(
            $"/api/gestion-logial/miembros/{memberId}/instrucciones",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, correctedHistory.StatusCode);
        var correctedJson = await correctedHistory.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, correctedJson.GetProperty("total").GetInt32());
        var correctedItem = correctedJson.GetProperty("items")[0];
        var instructionDateText = correctedItem.GetProperty("instructionDate").GetString();
        Assert.NotNull(instructionDateText);
        Assert.Equal(
            new DateOnly(2026, 9, 8),
            DateOnly.ParseExact(instructionDateText!, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        Assert.Equal(topic, correctedItem.GetProperty("topic").GetString());
        Assert.True(correctedItem.GetProperty("attended").GetBoolean());
        Assert.Equal(LodgeManagementCodes.InstructionAttendanceStatus.Present, correctedItem.GetProperty("status").GetString());

        var justifiedResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/instrucciones/{instructionId}/asistencia",
            new { items = new[] { new { memberId, status = LodgeManagementCodes.InstructionAttendanceStatus.Excused } } },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, justifiedResponse.StatusCode);

        var justifiedHistory = await client.GetAsync(
            $"/api/gestion-logial/miembros/{memberId}/instrucciones",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, justifiedHistory.StatusCode);
        var justifiedJson = await justifiedHistory.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var justifiedItem = justifiedJson.GetProperty("items")[0];
        Assert.False(justifiedItem.GetProperty("attended").GetBoolean());
        Assert.Equal(LodgeManagementCodes.InstructionAttendanceStatus.Excused, justifiedItem.GetProperty("status").GetString());

        var selfProfileResponse = await client.GetAsync("/api/member-self/profile", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, selfProfileResponse.StatusCode);
        var selfProfile = await selfProfileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var selfInstructions = selfProfile.GetProperty("activity").GetProperty("instruction");
        Assert.Contains(selfInstructions.GetProperty("history").EnumerateArray(), item =>
            item.GetProperty("topic").GetString() == topic &&
            item.GetProperty("attendanceStatus").GetString() == LodgeManagementCodes.InstructionAttendanceStatus.Excused);

        client.DefaultRequestHeaders.Remove("X-Test-Organization");
        client.DefaultRequestHeaders.Add("X-Test-Role", "grand_second_warden");
        var orderReport = await client.GetAsync("/api/gestion-logial/instrucciones/orden?grade=apprentice", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, orderReport.StatusCode);
        var reportJson = await orderReport.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var reportItem = reportJson.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("instructionId").GetGuid() == instructionId);
        Assert.Equal(0, reportItem.GetProperty("present").GetInt32());
        Assert.Equal(0, reportItem.GetProperty("absent").GetInt32());
        Assert.Equal(1, reportItem.GetProperty("excused").GetInt32());
        var summaryRow = reportJson.GetProperty("summary").EnumerateArray().Single(item =>
            item.GetProperty("organizationId").GetGuid() == organizationId &&
            item.GetProperty("grade").GetString() == LodgeManagementCodes.Grade.Apprentice);
        Assert.Equal(0, summaryRow.GetProperty("present").GetInt32());
        Assert.Equal(0, summaryRow.GetProperty("absent").GetInt32());
        Assert.Equal(1, summaryRow.GetProperty("excused").GetInt32());
        Assert.DoesNotContain(reportJson.GetProperty("items")[0].EnumerateObject(), property => property.Name.Contains("member", StringComparison.OrdinalIgnoreCase) || property.Name.Contains("displayName", StringComparison.OrdinalIgnoreCase));
        var otherGradeReport = await client.GetAsync("/api/gestion-logial/instrucciones/orden?grade=fellowcraft", cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, otherGradeReport.StatusCode);
        var editAttempt = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/instrucciones",
            new { instructionDate = new DateOnly(2026, 9, 9), grade = LodgeManagementCodes.Grade.Apprentice, topic = "Edición no autorizada" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, editAttempt.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var lodgeDb = verificationScope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
        var persistedAttendance = await lodgeDb.LodgeInstructionAttendanceRecords
            .AsNoTracking()
            .Where(x => x.InstructionSessionId == instructionId && x.MemberId == memberId)
            .OrderBy(x => x.RecordedAtUtc)
            .ToListAsync(cancellationToken);
        Assert.Equal(3, persistedAttendance.Count);
        Assert.Contains(persistedAttendance, x => x.Status == LodgeManagementCodes.InstructionAttendanceStatus.Absent);
        Assert.Contains(persistedAttendance, x => x.Status == LodgeManagementCodes.InstructionAttendanceStatus.Present);
        Assert.Contains(persistedAttendance, x => x.Status == LodgeManagementCodes.InstructionAttendanceStatus.Excused);

        var auditActions = await lodgeDb.AuditEvents
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.Action)
            .ToListAsync(cancellationToken);
        Assert.Contains("lodge.instruction.created", auditActions);
        Assert.Contains("lodge.instruction.completed", auditActions);
        Assert.Equal(3, auditActions.Count(x => x == "lodge.instruction.attendance_recorded"));
    }
}
