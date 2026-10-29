using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using MembershipEntity = PMGM.Api.Modules.Membership.Entities.Membership;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class MemberHistoryProposalHttpTests
{
    [Fact]
    public async Task Secretary_proposes_RI_approves_and_corrections_replace_with_audit()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId, memberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var organization = new Organization { Name = $"Taller Historia CI {Guid.NewGuid():N}", Number = "HIS-CI", Type = "workshop" };
            var member = new Member { Person = new Person { FirstNames = "Hermano", LastNames = "Historia CI" }, CurrentDegree = "master" };
            db.Organizations.Add(organization);
            db.Members.Add(member);
            db.Memberships.Add(new MembershipEntity { Member = member, Organization = organization, MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active });
            await db.SaveChangesAsync(ct);
            organizationId = organization.Id; memberId = member.Id;
        }
        var url = $"/api/secretaria/talleres/{organizationId}/hermanos/{memberId}/historia";

        // 1) La Secretaría propone cargos pasados y fechas.
        var propose = await client.PostAsJsonAsync(url, new
        {
            evidenceReference = "Acta N° 12 del Taller", reason = "Se completa la historia al migrar el Cuadro del Taller.",
            items = new object[]
            {
                new { kind = "office", action = "add", officeType = "orator", period = "2019", startDate = new DateOnly(2019, 1, 1), endDate = new DateOnly(2019, 12, 31) },
                new { kind = "degree", action = "add", eventType = "initiation", eventDate = new DateOnly(2008, 3, 10) },
                new { kind = "degree", action = "add", eventType = "wage_increase", eventDate = new DateOnly(2009, 4, 5) },
            },
        }, ct);
        Assert.Equal(HttpStatusCode.Created, propose.StatusCode);
        var proposalId = (await propose.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("id").GetGuid();

        // Nada cambia hasta que Régimen Interior apruebe; y no se acepta una segunda propuesta pendiente.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.False(await db.OfficeAssignments.AnyAsync(x => x.MemberId == memberId, ct));
            Assert.False(await db.DegreeEvents.AnyAsync(x => x.MemberId == memberId, ct));
        }
        var second = await client.PostAsJsonAsync(url, new { evidenceReference = "x", reason = "Otro intento de propuesta.", items = new object[] { new { kind = "degree", action = "add", eventType = "exaltation", eventDate = new DateOnly(2012, 1, 1) } } }, ct);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var pending = await client.GetFromJsonAsync<JsonElement>("/api/regimen-interior/historia-hermanos", ct);
        Assert.Contains(pending.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == proposalId);

        // 2) Régimen Interior aprueba: se aplica el historial.
        var approve = await client.PostAsJsonAsync($"/api/regimen-interior/historia-hermanos/{proposalId}/resolver", new { decision = "approve", notes = (string?)null }, ct);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Guid officeId, initiationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var office = await db.OfficeAssignments.SingleAsync(x => x.MemberId == memberId, ct);
            Assert.Equal("orator", office.OfficeType); Assert.Equal("2019", office.Period); Assert.Equal("Acta N° 12 del Taller", office.EvidenceReference);
            officeId = office.Id;
            var initiation = await db.DegreeEvents.SingleAsync(x => x.MemberId == memberId && x.EventType == MembershipCodes.DegreeEvent.Initiation, ct);
            Assert.Equal(new DateOnly(2008, 3, 10), initiation.EffectiveDate);
            initiationId = initiation.Id;
            Assert.True(await db.AuditEvents.AnyAsync(x => x.Action == "regimen_interior.member_history.approved" && x.EntityId == proposalId.ToString(), ct));
        }

        // 3) Corrección: reemplaza el registro vigente (no duplica) y queda auditada.
        var fix = await client.PostAsJsonAsync(url, new
        {
            evidenceReference = "Certificado de iniciación", reason = "La fecha de iniciación estaba mal digitada.",
            items = new object[]
            {
                new { kind = "degree", action = "replace", targetRecordId = initiationId, eventType = "initiation", eventDate = new DateOnly(2008, 3, 12) },
                new { kind = "office", action = "replace", targetRecordId = officeId, officeType = "orator", period = "2019-2020", startDate = new DateOnly(2019, 1, 1), endDate = new DateOnly(2020, 12, 31) },
            },
        }, ct);
        Assert.Equal(HttpStatusCode.Created, fix.StatusCode);
        var fixId = (await fix.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("id").GetGuid();
        var fixApprove = await client.PostAsJsonAsync($"/api/regimen-interior/historia-hermanos/{fixId}/resolver", new { decision = "approve" }, ct);
        Assert.Equal(HttpStatusCode.OK, fixApprove.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.Equal(1, await db.DegreeEvents.CountAsync(x => x.MemberId == memberId && x.EventType == MembershipCodes.DegreeEvent.Initiation, ct));
            Assert.Equal(new DateOnly(2008, 3, 12), (await db.DegreeEvents.SingleAsync(x => x.Id == initiationId, ct)).EffectiveDate);
            Assert.Equal("2019-2020", (await db.OfficeAssignments.SingleAsync(x => x.Id == officeId, ct)).Period);
            var audit = await db.AuditEvents.SingleAsync(x => x.Action == "regimen_interior.member_history.approved" && x.EntityId == fixId.ToString(), ct);
            Assert.Contains("before", audit.MetadataJson ?? string.Empty);
        }

        // 4) Observar o rechazar exige motivo y no cambia nada; una propuesta resuelta no se resuelve de nuevo.
        var again = await client.PostAsJsonAsync($"/api/regimen-interior/historia-hermanos/{fixId}/resolver", new { decision = "approve" }, ct);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var third = await client.PostAsJsonAsync(url, new { evidenceReference = "x", reason = "Propuesta para rechazar luego.", items = new object[] { new { kind = "degree", action = "add", eventType = "exaltation", eventDate = new DateOnly(2012, 1, 1) } } }, ct);
        var thirdId = (await third.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("id").GetGuid();
        var noReason = await client.PostAsJsonAsync($"/api/regimen-interior/historia-hermanos/{thirdId}/resolver", new { decision = "reject" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var reject = await client.PostAsJsonAsync($"/api/regimen-interior/historia-hermanos/{thirdId}/resolver", new { decision = "reject", notes = "Falta respaldo del acta." }, ct);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.False(await db.DegreeEvents.AnyAsync(x => x.MemberId == memberId && x.EventType == MembershipCodes.DegreeEvent.Exaltation, ct));
        }

        // Un hermano de otro Taller no admite propuestas desde este.
        var foreign = await client.PostAsJsonAsync($"/api/secretaria/talleres/{organizationId}/hermanos/{Guid.NewGuid()}/historia", new { evidenceReference = "x", reason = "Hermano ajeno al Taller.", items = new object[] { } }, ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
