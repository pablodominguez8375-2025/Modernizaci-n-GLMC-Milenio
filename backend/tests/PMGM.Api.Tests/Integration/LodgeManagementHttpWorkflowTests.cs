using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LodgeManagementHttpWorkflowTests
{
    [Fact]
    public async Task Meeting_attendance_correction_and_minute_versions_preserve_history()
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

            var organization = new Organization
            {
                Name = $"Taller Gestión Logial CI {Guid.NewGuid():N}",
                Number = "GL-CI",
                Type = "workshop"
            };
            var person = new Person { FirstNames = "Hermana", LastNames = "Gestión Logial" };
            var member = new Member
            {
                Person = person,
                PersonId = person.Id,
                InstitutionalNumber = $"GL-{Guid.NewGuid():N}"
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
            memberId = member.Id;
        }

        var memberOptions = await client.GetAsync($"/api/gestion-logial/talleres/{organizationId}/miembros/opciones", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, memberOptions.StatusCode);
        var memberOptionsText = await memberOptions.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("Hermana Gestión Logial", memberOptionsText);
        Assert.DoesNotContain("institutionalNumber", memberOptionsText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", memberOptionsText, StringComparison.OrdinalIgnoreCase);

        var meetingResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/talleres/{organizationId}/tenidas",
            new
            {
                meetingDate = new DateOnly(2026, 9, 8),
                meetingType = LodgeManagementCodes.MeetingType.Regular,
                grade = LodgeManagementCodes.Grade.Master,
                title = "Tenida ordinaria de integración"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, meetingResponse.StatusCode);
        var meetingJson = await meetingResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var meetingId = meetingJson.GetProperty("id").GetGuid();

        var presentResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/tenidas/{meetingId}/asistencia",
            new { memberId, status = LodgeManagementCodes.AttendanceStatus.Present, excuseReason = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, presentResponse.StatusCode);

        var correctedResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/tenidas/{meetingId}/asistencia",
            new { memberId, status = LodgeManagementCodes.AttendanceStatus.Excused, excuseReason = "Rectificación CI" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, correctedResponse.StatusCode);

        var attendanceResponse = await client.GetAsync($"/api/gestion-logial/tenidas/{meetingId}/asistencia", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, attendanceResponse.StatusCode);
        var attendanceJson = await attendanceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(1, attendanceJson.GetProperty("total").GetInt32());
        var attendanceItem = attendanceJson.GetProperty("items")[0];
        Assert.Equal(LodgeManagementCodes.AttendanceStatus.Excused, attendanceItem.GetProperty("status").GetString());
        Assert.Equal("Rectificación CI", attendanceItem.GetProperty("excuseReason").GetString());

        var minuteV1Response = await client.PostAsJsonAsync(
            $"/api/gestion-logial/tenidas/{meetingId}/actas",
            new { content = "Texto original del acta CI." },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, minuteV1Response.StatusCode);
        var minuteV1Json = await minuteV1Response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var minuteV1Id = minuteV1Json.GetProperty("id").GetGuid();
        Assert.Equal(1, minuteV1Json.GetProperty("version").GetInt32());

        var approveV1 = await client.PostAsync(
            $"/api/gestion-logial/tenidas/{meetingId}/actas/{minuteV1Id}/aprobar",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, approveV1.StatusCode);

        var minuteV2Response = await client.PostAsJsonAsync(
            $"/api/gestion-logial/tenidas/{meetingId}/actas",
            new { content = "Texto corregido del acta CI, conservando la versión anterior." },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, minuteV2Response.StatusCode);
        var minuteV2Json = await minuteV2Response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var minuteV2Id = minuteV2Json.GetProperty("id").GetGuid();
        Assert.Equal(2, minuteV2Json.GetProperty("version").GetInt32());

        var approveV2 = await client.PostAsync(
            $"/api/gestion-logial/tenidas/{meetingId}/actas/{minuteV2Id}/aprobar",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, approveV2.StatusCode);

        var minutesResponse = await client.GetAsync($"/api/gestion-logial/tenidas/{meetingId}/actas", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, minutesResponse.StatusCode);
        var minutesJson = await minutesResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(2, minutesJson.GetProperty("total").GetInt32());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var lodgeDb = verificationScope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();

        var persistedAttendance = await lodgeDb.LodgeAttendanceRecords
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId && x.MemberId == memberId)
            .OrderBy(x => x.RecordedAtUtc)
            .ToListAsync(cancellationToken);
        Assert.Equal(2, persistedAttendance.Count);
        Assert.Contains(persistedAttendance, x => x.Status == LodgeManagementCodes.AttendanceStatus.Present);
        Assert.Contains(persistedAttendance, x => x.Status == LodgeManagementCodes.AttendanceStatus.Excused);

        var persistedMinutes = await lodgeDb.LodgeMinutes
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.Version)
            .ToListAsync(cancellationToken);
        Assert.Equal(2, persistedMinutes.Count);
        Assert.Equal("Texto original del acta CI.", persistedMinutes[0].Content);
        Assert.Equal(LodgeManagementCodes.MinuteStatus.Superseded, persistedMinutes[0].Status);
        Assert.Equal("Texto corregido del acta CI, conservando la versión anterior.", persistedMinutes[1].Content);
        Assert.Equal(LodgeManagementCodes.MinuteStatus.Approved, persistedMinutes[1].Status);

        var auditActions = await lodgeDb.AuditEvents
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.Action)
            .ToListAsync(cancellationToken);
        Assert.Contains("lodge.meeting.created", auditActions);
        Assert.Equal(2, auditActions.Count(x => x == "lodge.attendance.recorded"));
        Assert.Equal(2, auditActions.Count(x => x == "lodge.minute.version_created"));
        Assert.Equal(2, auditActions.Count(x => x == "lodge.minute.approved"));
    }
}

internal sealed class LodgeManagementWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>();
            services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.RemoveAll<LodgeManagementDbContext>();
            services.RemoveAll<DbContextOptions<LodgeManagementDbContext>>();
            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<LodgeManagementDbContext>(options => options.UseNpgsql(connectionString));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = LodgeManagementTestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = LodgeManagementTestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = LodgeManagementTestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, LodgeManagementTestAuthenticationHandler>(
                    LodgeManagementTestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }
}

internal sealed class LodgeManagementTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PMGM-Lodge-CI-Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("sub", "ci-lodge-admin"),
            new Claim(ClaimTypes.NameIdentifier, "ci-lodge-admin"),
            new Claim(ClaimTypes.Name, "CI Lodge Admin"),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
