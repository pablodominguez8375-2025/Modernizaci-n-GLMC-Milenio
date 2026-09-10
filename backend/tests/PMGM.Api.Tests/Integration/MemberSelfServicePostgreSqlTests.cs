using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class MemberSelfServicePostgreSqlTests
{
    private const string TestIssuer = "urn:pmgm:unspecified-issuer";
    private const string TestSubject = "ci-http-admin";

    [Fact]
    public async Task Authenticated_member_can_read_and_update_only_own_contact_with_audit()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid memberId;
        Guid organizationId;
        string institutionalNumber;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM core.member_identity_links WHERE \"Issuer\" = {TestIssuer} AND \"Subject\" = {TestSubject}",
                cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Autoservicio {Guid.NewGuid():N}"[..45],
                Number = Guid.NewGuid().ToString("N")[..8],
                Type = "workshop"
            };
            var person = new Person
            {
                FirstNames = "Hermano",
                LastNames = "Autoservicio",
                Email = "antes@example.test",
                Phone = "+56 9 1111 1111",
                Address = "Domicilio anterior"
            };
            institutionalNumber = $"SELF-{Guid.NewGuid():N}"[..24];
            var member = new Member
            {
                Person = person,
                PersonId = person.Id,
                InstitutionalNumber = institutionalNumber
            };
            var membership = new Membership
            {
                Member = member,
                MemberId = member.Id,
                Organization = organization,
                OrganizationId = organization.Id,
                MembershipType = "regular",
                StartDate = new DateOnly(2020, 1, 10),
                Status = MembershipCodes.MembershipStatus.Active
            };
            var initiation = new DegreeEvent
            {
                Member = member,
                MemberId = member.Id,
                Organization = organization,
                OrganizationId = organization.Id,
                Degree = "1",
                EventType = MembershipCodes.DegreeEvent.Initiation,
                EffectiveDate = new DateOnly(2020, 1, 10)
            };
            var exaltation = new DegreeEvent
            {
                Member = member,
                MemberId = member.Id,
                Organization = organization,
                OrganizationId = organization.Id,
                Degree = "3",
                EventType = MembershipCodes.DegreeEvent.Exaltation,
                EffectiveDate = new DateOnly(2022, 6, 18)
            };
            var status = new InstitutionalStatusEvent
            {
                Member = member,
                MemberId = member.Id,
                Organization = organization,
                OrganizationId = organization.Id,
                EventType = MembershipCodes.InstitutionalStatus.Active,
                EffectiveDate = new DateOnly(2020, 1, 10)
            };
            var financial = new FinancialRegularitySnapshot
            {
                Organization = organization,
                OrganizationId = organization.Id,
                Member = member,
                MemberId = member.Id,
                Scope = "member",
                Status = "up_to_date",
                AsOfDate = new DateOnly(2026, 9, 10)
            };
            var hospitalaria = new HospitalariaRegularitySnapshot
            {
                Organization = organization,
                OrganizationId = organization.Id,
                Status = "up_to_date",
                AsOfDate = new DateOnly(2026, 9, 10)
            };

            db.AddRange(organization, person, member, membership, initiation, exaltation, status, financial, hospitalaria);
            await db.SaveChangesAsync(cancellationToken);

            memberId = member.Id;
            organizationId = organization.Id;
            var linkId = Guid.NewGuid();
            var createdAt = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links
                    ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject", "RevokedAtUtc")
                VALUES
                    ({linkId}, {member.Id}, {TestIssuer}, {TestSubject}, {createdAt}, {TestSubject}, NULL)
                """, cancellationToken);
        }

        var profileResponse = await client.GetAsync("/api/member-self/profile", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profileJson = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(memberId, profileJson.GetProperty("member").GetProperty("id").GetGuid());
        Assert.Equal(institutionalNumber, profileJson.GetProperty("member").GetProperty("institutionalNumber").GetString());
        Assert.Equal("antes@example.test", profileJson.GetProperty("contact").GetProperty("email").GetString());
        Assert.Equal(organizationId, profileJson.GetProperty("current").GetProperty("membership").GetProperty("organizationId").GetGuid());
        Assert.Equal(3, profileJson.GetProperty("current").GetProperty("effectiveDegree").GetInt32());
        Assert.Equal("up_to_date", profileJson.GetProperty("regularity").GetProperty("financial").GetProperty("status").GetString());

        var updateResponse = await client.PutAsJsonAsync(
            "/api/member-self/contact",
            new
            {
                email = "nuevo@example.test",
                phone = "+56 9 2222 2222",
                address = "Domicilio actualizado",
                institutionalNumber = "NO-DEBE-CAMBIAR",
                degree = "99"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var stored = await verificationDb.Members
            .AsNoTracking()
            .Where(x => x.Id == memberId)
            .Select(x => new
            {
                x.InstitutionalNumber,
                x.Person.Email,
                x.Person.Phone,
                x.Person.Address
            })
            .SingleAsync(cancellationToken);

        Assert.Equal(institutionalNumber, stored.InstitutionalNumber);
        Assert.Equal("nuevo@example.test", stored.Email);
        Assert.Equal("+56 9 2222 2222", stored.Phone);
        Assert.Equal("Domicilio actualizado", stored.Address);

        var latestDegree = await verificationDb.DegreeEvents
            .AsNoTracking()
            .Where(x => x.MemberId == memberId)
            .OrderByDescending(x => x.EffectiveDate)
            .Select(x => x.Degree)
            .FirstAsync(cancellationToken);
        Assert.Equal("3", latestDegree);

        var audit = await verificationDb.AuditEvents
            .AsNoTracking()
            .Where(x => x.EntityType == "member" && x.EntityId == memberId.ToString() && x.Action == "member.self.contact.updated")
            .OrderByDescending(x => x.OccurredAtUtc)
            .FirstAsync(cancellationToken);
        Assert.Equal("success", audit.Result);
        Assert.Contains("ChangedFields", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("nuevo@example.test", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Domicilio actualizado", audit.MetadataJson ?? string.Empty, StringComparison.Ordinal);
    }
}
