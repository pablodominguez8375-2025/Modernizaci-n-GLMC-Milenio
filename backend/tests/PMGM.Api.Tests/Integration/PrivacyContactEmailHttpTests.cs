using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Privacy;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PrivacyContactEmailHttpTests
{
    [Fact]
    public async Task Setting_rejects_multiple_recipients_at_http_boundary()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs);
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
        }
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var response = await client.PostAsJsonAsync(
            "/api/system/settings/system.privacy.contact_email",
            new
            {
                value = "uno@ejemplo.cl, dos@ejemplo.cl",
                effectiveFrom = today,
                sourceReference = "Prueba sintética"
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Contact_get_returns_default_or_active_version_without_system_settings_payload()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs);
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        await db.Database.MigrateAsync(ct);

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var versions = await db.InstitutionalRuleSettings
            .Where(x => x.Code == PrivacyContactPolicy.Code).ToListAsync(ct);
        db.InstitutionalRuleSettings.RemoveRange(versions);
        await db.SaveChangesAsync(ct);

        try
        {
            var original = await client.GetAsync("/api/privacy/contact", ct);
            Assert.Equal(HttpStatusCode.OK, original.StatusCode);
            var fallback = await original.Content.ReadFromJsonAsync<ContactReply>(
                cancellationToken: ct);
            Assert.Equal(PrivacyContactPolicy.DefaultEmail, fallback?.Email);

            var setting = new InstitutionalRuleSetting
            {
                Code = PrivacyContactPolicy.Code,
                Value = "derechos@ejemplo.cl",
                EffectiveFrom = today,
                Status = "active",
                SourceReference = "Plancha de prueba"
            };
            db.InstitutionalRuleSettings.Add(setting);
            await db.SaveChangesAsync(ct);
            var changed = await client.GetAsync("/api/privacy/contact", ct);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var reply = await changed.Content.ReadFromJsonAsync<ContactReply>(
                cancellationToken: ct);
            Assert.Equal("derechos@ejemplo.cl", reply?.Email);

            // Una versión futura no debe reemplazar todavía la vigente.
            db.InstitutionalRuleSettings.Add(new InstitutionalRuleSetting
            {
                Code = PrivacyContactPolicy.Code,
                Value = "futuro@ejemplo.cl",
                EffectiveFrom = today.AddDays(5),
                Status = "scheduled",
                SourceReference = "Cambio diferido de prueba"
            });
            await db.SaveChangesAsync(ct);
            var unchanged = await client.GetFromJsonAsync<ContactReply>(
                "/api/privacy/contact", ct);
            Assert.Equal("derechos@ejemplo.cl", unchanged?.Email);
        }
        finally
        {
            var created = await db.InstitutionalRuleSettings
                .Where(x => x.Code == PrivacyContactPolicy.Code).ToListAsync(ct);
            db.InstitutionalRuleSettings.RemoveRange(created);
            await db.SaveChangesAsync(ct);
            db.InstitutionalRuleSettings.AddRange(versions);
            await db.SaveChangesAsync(ct);
        }
    }

    private sealed record ContactReply(string Email);
}
