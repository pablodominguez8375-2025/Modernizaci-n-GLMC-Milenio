using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;
namespace PMGM.Api.Tests.Integration;
[Collection(PostgresIntegrationCollection.Name)]
public sealed class MemberHospitalariaV2HttpTests
{
    [Fact]
    public async Task Death_is_atomic_idempotent_and_self_history_is_private()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES"); if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connection); using var client = factory.CreateClient();
        var today = HospitalariaContributionGenerator.Today(); var deathDate = new DateOnly(1997,10,7);
        var org = new Organization { Name = "Hospitalaria v2 sintética", Type = "workshop" };
        Member M(string name) => new() { Person = new Person { FirstNames = name, LastNames = "Sintético" }, CurrentDegree = "3" };
        var own = M("Propio"); var other = M("Ajeno"); var dead = M("Fallecido"); var past = M("Past"); past.CurrentDegree = "past_active";
        Membership Join(Member m, string type = "regular") => new() { Member = m, Organization = org, Status = "active", MembershipType = type, StartDate = deathDate.AddYears(-1) };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await db.Database.MigrateAsync(ct);
            var lodge = scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>(); await lodge.Database.MigrateAsync(ct);
            db.AddRange(org, own, other, dead, past, Join(own), Join(other), Join(dead), Join(past,"past_active"));
            db.HospitalariaReplenishmentRates.Add(new HospitalariaReplenishmentRate { AmountPerActiveMember = 1500, EffectiveFrom = deathDate, EffectiveUntil = deathDate,
                DecreeNumber = "DEMO-354", DecreeDate = deathDate, SourceReference = "synthetic-decree.pdf", CreatedBySubject = "ci" });
            db.OfficeAssignments.AddRange(new OfficeAssignment { Member = own, Organization = org, OfficeType = "secretary", Period = "2025", StartDate = today.AddYears(-1) },
                new OfficeAssignment { Member = other, Organization = org, OfficeType = "PRIVATE-OTHER-OFFICE", Period = "2025" });
            db.DegreeEvents.Add(new DegreeEvent { Member = own, Organization = org, Degree = "3", EventType = "exaltation", EffectiveDate = today.AddYears(-1) });
            await db.SaveChangesAsync(ct);
            const string issuer = "urn:pmgm:unspecified-issuer", subject = "ci-http-admin";
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM core.member_identity_links WHERE \"Issuer\"={issuer} AND \"Subject\"={subject}", ct);
            var linkId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links ("Id","MemberId","Issuer","Subject","CreatedAtUtc","CreatedBySubject","RevokedAtUtc")
                VALUES ({linkId},{own.Id},{issuer},{subject},{now},{subject},NULL)
                """, ct);
            var meeting = new LodgeMeeting { OrganizationId = org.Id, MeetingDate = today, MeetingType = "solemn", CeremonyType = "initiation", Grade = "apprentice", Title = "Ceremonia propia", Status = "held" };
            lodge.AddRange(meeting, new LodgeAttendanceRecord { Meeting = meeting, MemberId = own.Id, Status = "excused" },
                new LodgeAttendanceRecord { Meeting = meeting, MemberId = other.Id, Status = "present", ExcuseReason = "PRIVATE-EXCUSE" });
            var session = new LodgeInstructionSession { OrganizationId = org.Id, InstructionDate = today, Grade = "master", Topic = "Instrucción propia", ResponsibleOffice = "past_master", Status = "held", CreatedBySubject = "ci" };
            lodge.AddRange(session, new LodgeInstructionAttendanceRecord { InstructionSession = session, MemberId = own.Id, Status = "present", RecordedBySubject = "ci" });
            await lodge.SaveChangesAsync(ct);
        }
        var candidatesBefore=await client.GetFromJsonAsync<JsonElement>($"/api/hospitalaria/talleres/{org.Id}/defunciones/candidatos",ct);
        Assert.Equal(4,candidatesBefore.GetProperty("items").GetArrayLength());
        var request = new { memberId = dead.Id, deathDate, evidenceReference = "synthetic-death.pdf" };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/hospitalaria/talleres/{org.Id}/defunciones", request, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/hospitalaria/talleres/{org.Id}/defunciones", request, ct)).StatusCode);
        var candidatesAfter=await client.GetFromJsonAsync<JsonElement>($"/api/hospitalaria/talleres/{org.Id}/defunciones/candidatos",ct);
        Assert.Equal(3,candidatesAfter.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain(dead.Id.ToString(),candidatesAfter.GetRawText());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var c = Assert.Single(await db.DeathReplenishmentCases.Include(x=>x.Obligations).Where(x=>x.DeceasedMemberId==dead.Id).ToListAsync(ct));
            Assert.Equal(2,c.Obligations.Count(x=>x.OrganizationId==org.Id)); Assert.All(c.Obligations,x=>Assert.Equal(1500,x.AmountDue));
            Assert.DoesNotContain(c.Obligations,x=>x.MemberId==past.Id||x.MemberId==dead.Id);
            Assert.True(await db.AuditEvents.AnyAsync(x=>x.Action=="hospitalaria.death_replenishment.generated"&&x.EntityId==c.Id.ToString(),ct));
        }
        foreach(var path in new[]{"cargos","asistencias","hospitalaria"})
        {
            var r=await client.GetAsync($"/api/membership/me/{path}?memberId={other.Id}",ct); Assert.Equal(HttpStatusCode.OK,r.StatusCode);
            Assert.True(r.Headers.CacheControl!.Private); Assert.True(r.Headers.CacheControl.NoStore);
            var text=await r.Content.ReadAsStringAsync(ct); Assert.DoesNotContain("PRIVATE-OTHER",text); Assert.DoesNotContain("PRIVATE-EXCUSE",text);
            if(path=="hospitalaria") { Assert.Single(JsonSerializer.Deserialize<JsonElement>(text).GetProperty("items").EnumerateArray()); Assert.Contains("DEMO-354",text); }
        }
        var attendance=await client.GetFromJsonAsync<JsonElement>("/api/membership/me/asistencias?tipo=ceremonia",ct);
        Assert.Equal(1,attendance.GetProperty("resumen").GetProperty("justificado").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/membership/me/asistencias?desde=2026-10-08&hasta=2026-10-07",ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/membership/me/asistencias?tipo=private",ct)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,(await client.PostAsync("/api/membership/me/cargos",null,ct)).StatusCode);
        await using var revoke=factory.Services.CreateAsyncScope(); var revokeDb=revoke.ServiceProvider.GetRequiredService<PmgmDbContext>();
        const string ownSubject="ci-http-admin",ownIssuer="urn:pmgm:unspecified-issuer";
        await revokeDb.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM core.member_identity_links WHERE \"Subject\"={ownSubject} AND \"Issuer\"={ownIssuer}",ct);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/membership/me/hospitalaria",ct)).StatusCode);
    }
    [Fact]
    public async Task Contribution_is_per_workshop_and_reconciled_separately()
    {
        var connection=Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES"); if(string.IsNullOrWhiteSpace(connection))return;
        var ct=TestContext.Current.CancellationToken; using var factory=new PmgmWebApplicationFactory(connection); using var client=factory.CreateClient();
        var today=HospitalariaContributionGenerator.Today(); var org=new Organization {Name="Aporte mensual sintético",Type="workshop"};
        await using(var scope=factory.Services.CreateAsyncScope())
        {var db=scope.ServiceProvider.GetRequiredService<PmgmDbContext>();await db.Database.MigrateAsync(ct);db.Organizations.Add(org);await db.SaveChangesAsync(ct);await HospitalariaContributionGenerator.GenerateAsync(db,ct);await HospitalariaContributionGenerator.GenerateAsync(db,ct);}
        var list=await client.GetFromJsonAsync<JsonElement>($"/api/hospitalaria/aportes?organizationId={org.Id}",ct);
        var item=Assert.Single(list.GetProperty("items").EnumerateArray());Assert.Equal(6000,item.GetProperty("amountDue").GetDecimal());var id=item.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/hospitalaria/aportes/{id}/pago",new{amount=1500,paymentDate=today,reference="synthetic"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync($"/api/hospitalaria/aportes/{id}/pago",new{amount=6000,paymentDate=today,reference="synthetic"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/hospitalaria/aportes/{id}/pago",new{amount=6000,paymentDate=today,reference="duplicate"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync($"/api/hospitalaria/aportes/{id}/revision",new{decision="reconciled"},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/hospitalaria/aportes/tarifas",new{amount=7000,effectiveFrom=today.AddDays(-1)},ct)).StatusCode);
        await using var verify=factory.Services.CreateAsyncScope();var vdb=verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal("reconciled",(await vdb.HospitalariaContributionObligations.SingleAsync(x=>x.Id==id,ct)).Status);
        Assert.False(await vdb.DeathReplenishmentObligations.AnyAsync(x=>x.OrganizationId==org.Id,ct));
    }
}
