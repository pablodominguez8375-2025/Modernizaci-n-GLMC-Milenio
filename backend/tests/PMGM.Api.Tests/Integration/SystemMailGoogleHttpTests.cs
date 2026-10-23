using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.SystemConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class SystemMailGoogleHttpTests
{
    private static readonly string[] MailCodes =
    [
        GoogleSmtpPolicy.SecretCode, GoogleSmtpPolicy.HostCode,
        GoogleSmtpPolicy.PortCode, GoogleSmtpPolicy.AddressCode,
        GoogleSmtpPolicy.NameCode, GoogleSmtpPolicy.SecurityCode
    ];

    [Fact]
    public async Task Admin_stores_only_encrypted_secret_and_tests_delivery_without_network()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var sender = new CapturingMailSender();
        var serverKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var factory = new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SystemMail:EncryptionKey"] = serverKey,
                        ["RequestRateLimiting:SmtpTestsPerMinute"] = "5"
                    }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ISystemMailSender>();
                    services.AddSingleton<ISystemMailSender>(sender);
                });
            });
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        await db.Database.MigrateAsync(ct);
        var prior = await db.InstitutionalRuleSettings
            .Where(x => MailCodes.Contains(x.Code)).ToArrayAsync(ct);
        db.InstitutionalRuleSettings.RemoveRange(prior);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        const string mailbox = "emisor-prueba@example.org";
        const string recipient = "casilla-prueba@example.org";
        const string rawSecret = "smtp-credencial-ficticia-1234";
        var chileToday = DateOnly.FromDateTime(TimeZoneInfo
            .ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,
                "America/Santiago").DateTime);
        try
        {
            using (var before = await client.GetAsync(
                "/api/system/mail/credentials", ct))
            {
                Assert.Equal(HttpStatusCode.OK, before.StatusCode);
                var status = await before.Content.ReadFromJsonAsync<JsonElement>(
                    cancellationToken: ct);
                Assert.False(status.GetProperty("configured").GetBoolean());
                Assert.True(status.GetProperty("encryptionReady").GetBoolean());
            }

            void AddSetting(string code, string value) =>
                db.InstitutionalRuleSettings.Add(new InstitutionalRuleSetting
                {
                    Code = code, Value = value, EffectiveFrom = chileToday,
                    Status = "active", SourceReference = "QA SMTP sintética"
                });
            AddSetting(GoogleSmtpPolicy.HostCode, "smtp.gmail.com");
            AddSetting(GoogleSmtpPolicy.PortCode, "587");
            AddSetting(GoogleSmtpPolicy.AddressCode, mailbox);
            AddSetting(GoogleSmtpPolicy.NameCode, "Notificaciones QA");
            AddSetting(GoogleSmtpPolicy.SecurityCode, "STARTTLS");
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            using (var invalid = await client.PostAsJsonAsync(
                "/api/system/mail/credentials",
                new { password = "corta" }, ct))
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

            using (var saved = await client.PostAsJsonAsync(
                "/api/system/mail/credentials",
                new { password = rawSecret }, ct))
            {
                Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
                Assert.DoesNotContain(rawSecret, await saved.Content.ReadAsStringAsync(ct));
                Assert.Contains("no-store", saved.Headers.CacheControl?.ToString() ?? "");
            }
            var encrypted = await db.InstitutionalRuleSettings.AsNoTracking()
                .SingleAsync(x => x.Code == GoogleSmtpPolicy.SecretCode, ct);
            Assert.StartsWith("g1:", encrypted.Value);
            Assert.DoesNotContain(rawSecret, encrypted.Value);

            using (var statusResponse = await client.GetAsync(
                "/api/system/mail/credentials", ct))
            {
                var text = await statusResponse.Content.ReadAsStringAsync(ct);
                Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
                Assert.Contains("\"configured\":true", text);
                Assert.DoesNotContain(rawSecret, text);
                Assert.DoesNotContain(encrypted.Value, text);
            }
            using (var catalog = await client.GetAsync(
                "/api/system/settings/", ct))
            {
                var text = await catalog.Content.ReadAsStringAsync(ct);
                Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
                Assert.DoesNotContain(GoogleSmtpPolicy.SecretCode, text);
                Assert.DoesNotContain(rawSecret, text);
                Assert.DoesNotContain(encrypted.Value, text);
            }
            using (var history = await client.GetAsync(
                "/api/system/settings/" + GoogleSmtpPolicy.SecretCode + "/versions", ct))
                Assert.Equal(HttpStatusCode.BadRequest, history.StatusCode);

            using (var invalidAddress = await client.PostAsJsonAsync(
                "/api/system/mail/test", new { recipient = "incorrecto" }, ct))
                Assert.Equal(HttpStatusCode.BadRequest, invalidAddress.StatusCode);

            // Test inyecta un sender falso: NUNCA abre conexión SMTP externa.
            for (var i = 0; i < 5; i++)
            {
                using var response = await client.PostAsJsonAsync(
                    "/api/system/mail/test", new { recipient }, ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(
                    cancellationToken: ct);
                Assert.True(body.GetProperty("sent").GetBoolean());
            }
            using (var sixth = await client.PostAsJsonAsync(
                "/api/system/mail/test", new { recipient }, ct))
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
            }
            Assert.Equal(5, sender.CallCount);
            Assert.Equal(recipient, sender.LastRecipient);
            Assert.Equal(mailbox, sender.LastAccount);
            Assert.Equal(rawSecret, sender.LastPassword);

            var audit = await db.AuditEvents.AsNoTracking()
                .Where(x => (x.Action == "system.mail.credential_updated" ||
                              x.Action == "system.mail.delivery_tested") &&
                             x.ActorSubject == "ci-http-admin")
                .OrderByDescending(x => x.OccurredAtUtc).Take(12).ToListAsync(ct);
            Assert.Contains(audit, e => e.Action == "system.mail.credential_updated");
            Assert.Contains(audit, e => e.Action == "system.mail.delivery_tested");
            Assert.DoesNotContain(audit, e =>
                (e.MetadataJson ?? "").Contains(rawSecret, StringComparison.Ordinal) ||
                (e.MetadataJson ?? "").Contains(recipient, StringComparison.Ordinal));
        }
        finally
        {
            db.ChangeTracker.Clear();
            var made = await db.InstitutionalRuleSettings
                .Where(x => MailCodes.Contains(x.Code)).ToArrayAsync(ct);
            db.InstitutionalRuleSettings.RemoveRange(made);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            db.InstitutionalRuleSettings.AddRange(prior);
            await db.SaveChangesAsync(ct);
        }
    }

    private sealed class CapturingMailSender : ISystemMailSender
    {
        public int CallCount { get; private set; }
        public string? LastPassword { get; private set; }
        public string? LastAccount { get; private set; }
        public string? LastRecipient { get; private set; }

        public Task SendAsync(GoogleSmtpConfiguration configuration, string recipient,
            string subject, string body, CancellationToken ct)
        {
            CallCount++;
            LastPassword = configuration.Password;
            LastAccount = configuration.Username;
            LastRecipient = recipient;
            Assert.Equal("smtp.gmail.com", configuration.Host);
            Assert.Equal(587, configuration.Port);
            Assert.DoesNotContain(configuration.Password, body);
            return Task.CompletedTask;
        }
    }
}
