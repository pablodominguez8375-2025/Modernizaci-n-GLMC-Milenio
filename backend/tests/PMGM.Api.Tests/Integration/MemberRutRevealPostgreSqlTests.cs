using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class MemberRutRevealPostgreSqlTests
{
    [Fact]
    public async Task Third_party_rut_is_revealed_only_with_valid_reason_and_persisted_audit()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid memberId;
        // RUT válido único por ejecución (cuerpo de 8 dígitos + DV módulo 11).
        var body = (30_000_000 + Random.Shared.Next(0, 9_000_000)).ToString();
        var sum = 0; for (var i = 0; i < body.Length; i++) sum += (body[^(i + 1)] - '0') * (2 + (i % 6));
        var d = 11 - (sum % 11); var dv = d == 11 ? "0" : d == 10 ? "K" : d.ToString();
        var rut = $"{body}-{dv}";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var org = new Organization { Name = $"Taller RUT {Guid.NewGuid():N}"[..40], Number = Guid.NewGuid().ToString("N")[..8], Type = "workshop" };
            var person = new Person { FirstNames = "Hermano", LastNames = "Revelado", Rut = rut };
            var member = new Member { Person = person, PersonId = person.Id, InstitutionalNumber = $"RUT-{Guid.NewGuid():N}"[..20] };
            db.Add(new Membership { Member = member, MemberId = member.Id, Organization = org, OrganizationId = org.Id, MembershipType = "regular", StartDate = new DateOnly(2020, 1, 10), Status = MembershipCodes.MembershipStatus.Active });
            await db.SaveChangesAsync(ct);
            memberId = member.Id;
        }

        using var noReason = await client.PostAsJsonAsync($"/api/members/{memberId}/rut/reveal", new { reason = "" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        using var badReason = await client.PostAsJsonAsync($"/api/members/{memberId}/rut/reveal", new { reason = "curiosidad" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, badReason.StatusCode);

        using var ok = await client.PostAsJsonAsync($"/api/members/{memberId}/rut/reveal", new { reason = "identity_verification" }, ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Contains("no-store", ok.Headers.CacheControl?.ToString());
        var json = await ok.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(15, json.GetProperty("expiresInSeconds").GetInt32());
        Assert.Contains("-", json.GetProperty("rut").GetString());

        using var missing = await client.PostAsJsonAsync($"/api/members/{Guid.NewGuid()}/rut/reveal", new { reason = "identity_verification" }, ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        await using var check = factory.Services.CreateAsyncScope();
        var audit = check.ServiceProvider.GetRequiredService<PmgmDbContext>().AuditEvents
            .Where(x => x.Action == "member.rut.revealed" && x.EntityId == memberId.ToString());
        Assert.Equal(1, await audit.CountAsync(ct));
        var raw = await audit.Select(x => x.MetadataJson).SingleAsync(ct);
        Assert.DoesNotContain(rut, raw ?? "", StringComparison.Ordinal);
        Assert.DoesNotContain(body, raw ?? "", StringComparison.Ordinal);
    }
}
