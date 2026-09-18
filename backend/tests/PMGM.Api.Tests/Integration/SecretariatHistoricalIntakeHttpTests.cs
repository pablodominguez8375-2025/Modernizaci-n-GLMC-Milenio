using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class SecretariatHistoricalIntakeHttpTests
{
    [Fact]
    public async Task Secretary_submits_existing_active_brother_and_RI_approval_updates_official_cuadro()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Carga Histórica CI {Guid.NewGuid():N}",
                Number = "HIST-CI",
                Type = "workshop"
            };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync(cancellationToken);
            organizationId = organization.Id;
        }

        var create = await client.PostAsJsonAsync(
            $"/api/secretaria/talleres/{organizationId}/cuadro/carga-historica",
            new
            {
                firstNames = "Hermana",
                lastNames = "Histórica CI",
                rut = "12345678-5",
                institutionalNumber = $"GLM-{Guid.NewGuid():N}"[..16],
                email = "historica-ci@example.invalid",
                phone = "+56 9 0000 0000",
                currentDegree = "master",
                membershipStartDate = (DateOnly?)null,
                initiationDate = new DateOnly(1998, 5, 12),
                wageIncreaseDate = (DateOnly?)null,
                exaltationDate = new DateOnly(2002, 8, 24),
                cutoffDate = new DateOnly(2026, 9, 18),
                evidenceReference = "Cuadro del Taller histórico CI",
                offices = new[]
                {
                    new
                    {
                        officeType = "lodge_secretariat",
                        period = "2026",
                        startDate = (DateOnly?)null,
                        endDate = (DateOnly?)null,
                        isCurrent = true
                    }
                }
            },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var intakeId = created.GetProperty("id").GetGuid();
        Assert.Equal("draft", created.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("membershipStartDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("wageIncreaseDate").ValueKind);

        var submit = await client.PostAsync(
            $"/api/secretaria/carga-historica/{intakeId}/enviar-ri",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.False(await db.Memberships.AnyAsync(
                x => x.OrganizationId == organizationId &&
                     x.Member.Person.FirstNames == "Hermana" &&
                     x.Member.Person.LastNames == "Histórica CI",
                cancellationToken));
        }

        var review = await client.PostAsJsonAsync(
            $"/api/regimen-interior/carga-historica/{intakeId}/resolver",
            new { decision = "approve", notes = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var membership = await db.Memberships
                .Include(x => x.Member).ThenInclude(x => x.Person)
                .SingleAsync(
                    x => x.OrganizationId == organizationId &&
                         x.Member.Person.FirstNames == "Hermana" &&
                         x.Member.Person.LastNames == "Histórica CI",
                    cancellationToken);

            Assert.Equal(MembershipCodes.MembershipStatus.Active, membership.Status);
            Assert.Null(membership.StartDate);
            Assert.Equal("master", membership.Member.CurrentDegree);
            Assert.Equal("12345678-5", membership.Member.Person.Rut);

            var events = await db.DegreeEvents
                .Where(x => x.MemberId == membership.MemberId)
                .OrderBy(x => x.EffectiveDate)
                .ToListAsync(cancellationToken);

            Assert.Contains(events, x => x.EventType == MembershipCodes.DegreeEvent.Initiation && x.EffectiveDate == new DateOnly(1998, 5, 12));
            Assert.Contains(events, x => x.EventType == MembershipCodes.DegreeEvent.Exaltation && x.EffectiveDate == new DateOnly(2002, 8, 24));
            Assert.DoesNotContain(events, x => x.EventType == MembershipCodes.DegreeEvent.WageIncrease);

            var office = await db.OfficeAssignments.SingleAsync(
                x => x.MemberId == membership.MemberId &&
                     x.OrganizationId == organizationId &&
                     x.OfficeType == "lodge_secretariat",
                cancellationToken);
            Assert.Null(office.StartDate);
            Assert.Null(office.EndDate);
        }
    }

    [Fact]
    public async Task RI_observation_does_not_mutate_official_cuadro()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            var organization = new Organization { Name = $"Taller Observación CI {Guid.NewGuid():N}", Number = "OBS-CI", Type = "workshop" };
            db.Add(organization);
            await db.SaveChangesAsync(cancellationToken);
            organizationId = organization.Id;
        }

        var create = await client.PostAsJsonAsync(
            $"/api/secretaria/talleres/{organizationId}/cuadro/carga-historica",
            new
            {
                firstNames = "Hermano", lastNames = "Observado CI", rut = (string?)null,
                institutionalNumber = $"OBS-{Guid.NewGuid():N}"[..16], email = (string?)null, phone = (string?)null,
                currentDegree = "fellowcraft", membershipStartDate = (DateOnly?)null,
                initiationDate = (DateOnly?)null, wageIncreaseDate = (DateOnly?)null, exaltationDate = (DateOnly?)null,
                cutoffDate = new DateOnly(2026, 9, 18), evidenceReference = "Libro de Secretaría CI", offices = Array.Empty<object>()
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var json = await create.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var intakeId = json.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/secretaria/carga-historica/{intakeId}/enviar-ri", null, cancellationToken)).StatusCode);
        var observed = await client.PostAsJsonAsync(
            $"/api/regimen-interior/carga-historica/{intakeId}/resolver",
            new { decision = "observe", notes = "Falta respaldo institucional." },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, observed.StatusCode);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.False(await verify.Memberships.AnyAsync(
            x => x.OrganizationId == organizationId && x.Member.Person.LastNames == "Observado CI",
            cancellationToken));
    }
}
