using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Notifications.Entities;
using PMGM.Api.Modules.UserAccounts;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>#574 etapa 2 — «Corregir como soporte»: permiso del Máster con vigencia, corrección puntual con motivo, auditoría del operador y aviso al afectado.</summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class SupportCorrectionHttpTests
{
    private const string Issuer = "https://identity.example.invalid/test";
    private const string MasterSub = "master-synthetic-subject";
    private static readonly string Hermano = Guid.NewGuid().ToString();
    private static readonly string Admin = Guid.NewGuid().ToString();
    private static readonly string Superusuario = Guid.NewGuid().ToString();
    private static readonly string OtroSuper = Guid.NewGuid().ToString();

    [Fact]
    public async Task Only_the_registered_master_grants_and_the_superuser_corrects_contact_with_reason_and_audit()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateAsync(cs);
        var memberId = await SeedMemberAsync(factory, ct);
        using var master = factory.CreateClient(); master.DefaultRequestHeaders.Add("X-Who", "master");
        using var op = factory.CreateClient(); op.DefaultRequestHeaders.Add("X-Who", "super");
        using var other = factory.CreateClient(); other.DefaultRequestHeaders.Add("X-Who", "other");

        // Sin permiso vigente: el Superusuario no corrige.
        Assert.Equal(HttpStatusCode.Forbidden, (await Correct(op, Hermano, "phone", "+56 9 1111 2222", "Teléfono mal digitado", ct)).StatusCode);
        // Un Superusuario no se da permiso a sí mismo ni a otro; solo el Máster registrado.
        Assert.Equal(HttpStatusCode.Forbidden, (await Grant(op, Superusuario, "Me doy permiso a mí mismo", 60, ct)).StatusCode);
        // El Máster: motivo obligatorio, destino válido (solo Superusuario), vigencia acotada.
        Assert.Equal(HttpStatusCode.BadRequest, (await Grant(master, Superusuario, "corto", 60, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Grant(master, Superusuario, "Corregir contactos de la semana", 600, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Grant(master, Hermano, "Un Hermano no es operador", 60, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Grant(master, Admin, "Un administrador no es operador", 60, ct)).StatusCode);
        var granted = await Grant(master, Superusuario, "Corregir contactos reportados esta semana", 60, ct);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        var grantId = (await granted.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("grantId").GetGuid();

        // Con permiso: lista cerrada de campos, motivo y valor válido.
        Assert.Equal(HttpStatusCode.BadRequest, (await Correct(op, Hermano, "rut", "11.111.111-1", "No se puede corregir el RUT", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Correct(op, Hermano, "phone", "+56 9 1111 2222", "corto", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Correct(op, Hermano, "email", "sin-arroba", "Correo mal escrito", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Correct(op, Admin, "phone", "+56 9 1111 2222", "Cuenta de administración", ct)).StatusCode);
        // El Máster da el permiso pero no corrige.
        Assert.Equal(HttpStatusCode.Forbidden, (await Correct(master, Hermano, "phone", "+56 9 1111 2222", "El Máster no corrige", ct)).StatusCode);
        // Otro Superusuario sin permiso propio tampoco.
        Assert.Equal(HttpStatusCode.Forbidden, (await Correct(other, Hermano, "phone", "+56 9 1111 2222", "Sin permiso propio", ct)).StatusCode);

        var ok = await Correct(op, Hermano, "phone", "+56 9 1111 2222", "Teléfono mal digitado en la ficha", ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("corrected", (await ok.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("status").GetString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var person = (await db.Members.Include(x => x.Person).FirstAsync(x => x.Id == memberId, ct)).Person;
            Assert.Equal("+56 9 1111 2222", person.Phone);
            var audits = await db.AuditEvents.Where(a => a.Action.StartsWith("support.correction")).ToListAsync(ct);
            Assert.Contains(audits, a => a.Action == "support.correction.applied" && a.ActorSubject == Superusuario);
            Assert.Contains(audits, a => a.Action == "support.correction_grant.created" && a.ActorSubject == MasterSub);
            // Nunca se audita el valor del dato personal.
            Assert.All(audits, a => Assert.DoesNotContain("1111 2222", a.MetadataJson ?? ""));
        }

        // El Hermano afectado ve qué se corrigió, por quién y por qué (sin el valor).
        using var brother = factory.CreateClient(); brother.DefaultRequestHeaders.Add("X-Who", "brother");
        var mine = await brother.GetFromJsonAsync<JsonElement>("/api/session/support-corrections", ct);
        var item = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal("Teléfono", item.GetProperty("label").GetString());
        Assert.Equal("Soporte Ficticio", item.GetProperty("operatorName").GetString());
        Assert.DoesNotContain("1111 2222", item.ToString());
        // Y otra persona no ve nada.
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/session/support-corrections", ct)).GetProperty("items").EnumerateArray());

        // Revocado o vencido: se acaba el permiso.
        Assert.Equal(HttpStatusCode.OK, (await master.PostAsync($"/api/system/support/corrections/grants/{grantId}/revoke", null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Correct(op, Hermano, "phone", "+56 9 3333 4444", "Intento tras revocar", ct)).StatusCode);
    }

    [Fact]
    public async Task Resend_notice_goes_to_the_same_email_only_for_internal_notices_with_cooldown_reason_and_audit()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = await CreateAsync(cs);
        await SeedMemberAsync(factory, ct);
        Guid internalId, restrictedId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var n = scope.ServiceProvider.GetRequiredService<PMGM.Api.Data.NotificationDbContext>();
            var template = new NotificationTemplate { Code = "support.test." + Guid.NewGuid().ToString("N"), Name = "Plantilla de prueba", BodyTemplate = "Cuerpo" };
            n.NotificationTemplates.Add(template);
            NotificationMessage Msg(string classification) => new()
            {
                NotificationTemplateId = template.Id, TypeCode = "candidate.interview.assigned", RecipientSubject = Hermano, RecipientEmail = "hermano.correccion@example.invalid",
                Subject = "Entrevista asignada", Body = "Cuerpo", Classification = classification, IdempotencyKey = Guid.NewGuid().ToString("N"), CorrelationId = "t"
            };
            var a = Msg("internal"); var b = Msg("restricted");
            n.NotificationMessages.AddRange(a, b); await n.SaveChangesAsync(ct);
            internalId = a.Id; restrictedId = b.Id;
        }
        using var master = factory.CreateClient(); master.DefaultRequestHeaders.Add("X-Who", "master");
        using var op = factory.CreateClient(); op.DefaultRequestHeaders.Add("X-Who", "super");
        using var brother = factory.CreateClient(); brother.DefaultRequestHeaders.Add("X-Who", "brother");

        Assert.Equal(HttpStatusCode.Forbidden, (await Resend(op, internalId, "Sin permiso vigente todavía", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Grant(master, Superusuario, "Reenviar avisos reportados esta semana", 60, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Resend(master, internalId, "El Máster no reenvía avisos", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Resend(op, internalId, "corto", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Resend(op, Guid.NewGuid(), "Aviso que no existe en la base", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Resend(op, restrictedId, "Aviso reservado no reenviable", ct)).StatusCode);

        var list = await op.GetFromJsonAsync<JsonElement>($"/api/system/support/corrections/notices?targetSubject={Hermano}", ct);
        var only = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(internalId, only.GetProperty("messageId").GetGuid());

        var ok = await Resend(op, internalId, "El Hermano no recibió el correo", ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        // Segundo reenvío inmediato: bloqueado.
        Assert.Equal((HttpStatusCode)429, (await Resend(op, internalId, "Reintento inmediato del reenvío", ct)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var n = scope.ServiceProvider.GetRequiredService<PMGM.Api.Data.NotificationDbContext>();
            var d = await n.NotificationDeliveries.Where(x => x.NotificationMessageId == internalId).ToListAsync(ct);
            var queued = Assert.Single(d);
            Assert.Equal("email", queued.Channel); Assert.Equal("queued", queued.Status);
            var audits = await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().AuditEvents.Where(a => a.Action.StartsWith("support.resend")).ToListAsync(ct);
            Assert.Contains(audits, a => a.Action == "support.resend.applied" && a.ActorSubject == Superusuario);
            Assert.All(audits, a => Assert.DoesNotContain("example.invalid", a.MetadataJson ?? ""));
        }
        var mine = await brother.GetFromJsonAsync<JsonElement>("/api/session/support-corrections", ct);
        var item = Assert.Single(mine.GetProperty("items").EnumerateArray());
        Assert.Equal("Aviso reenviado", item.GetProperty("label").GetString());
        Assert.Equal("Entrevista asignada", item.GetProperty("detail").GetString());
        Assert.Equal("Soporte Ficticio", item.GetProperty("operatorName").GetString());
    }

    private static Task<HttpResponseMessage> Resend(HttpClient c, Guid messageId, string reason, CancellationToken ct)
        => c.PostAsJsonAsync("/api/system/support/corrections/resend", new { targetSubject = Hermano, messageId, reason }, ct);

    private static Task<HttpResponseMessage> Grant(HttpClient c, string operatorSubject, string reason, int minutes, CancellationToken ct)
        => c.PostAsJsonAsync("/api/system/support/corrections/grants", new { operatorSubject, reason, minutes }, ct);
    private static Task<HttpResponseMessage> Correct(HttpClient c, string target, string field, string value, string reason, CancellationToken ct)
        => c.PostAsJsonAsync("/api/system/support/corrections", new { targetSubject = target, field, newValue = value, reason }, ct);

    private static async Task<Guid> SeedMemberAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var person = new Person { FirstNames = "Hermano", LastNames = "Ficticio Corrección", Email = "hermano.correccion@example.invalid", Phone = "+56 9 0000 0000" };
        var member = new Member { PersonId = person.Id, Person = person };
        db.Members.Add(member);
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlAsync($"UPDATE core.member_identity_links SET \"RevokedAtUtc\" = {DateTimeOffset.UtcNow} WHERE \"Subject\" = {Hermano} AND \"RevokedAtUtc\" IS NULL", ct);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO core.member_identity_links (\"Id\", \"MemberId\", \"Issuer\", \"Subject\", \"CreatedAtUtc\", \"CreatedBySubject\") VALUES ({Guid.NewGuid()}, {member.Id}, {Issuer}, {Hermano}, {DateTimeOffset.UtcNow}, {"test"})", ct);
        return member.Id;
    }

    private static async Task<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>> CreateAsync(string cs)
    {
        var factory = new PmgmWebApplicationFactory(cs).WithWebHostBuilder(b =>
        {
            b.UseSetting("Authentication:Authority", Issuer);
            b.UseSetting("InitialLoad:MasterSubject", MasterSub);
            b.UseSetting("InitialLoad:MasterIssuer", Issuer);
            b.ConfigureTestServices(services =>
            {
                services.AddSingleton<IAccountIdentityProvider>(new Identity());
                services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = Auth.Name; o.DefaultChallengeScheme = Auth.Name; o.DefaultForbidScheme = Auth.Name;
                }).AddScheme<AuthenticationSchemeOptions, Auth>(Auth.Name, _ => { });
            });
        });
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<PMGM.Api.Data.NotificationDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE core.support_correction_grants SET \"RevokedAtUtc\" = now() WHERE \"RevokedAtUtc\" IS NULL", TestContext.Current.CancellationToken);
        return factory;
    }

    private sealed class Identity : IAccountIdentityProvider
    {
        public bool Configured => true; public string Issuer => SupportCorrectionHttpTests.Issuer;
        public Task<PreparedIdentity> PrepareDisabledAsync(AccountIdentity account, string password, CancellationToken ct) => throw new NotSupportedException();
        public Task EnableAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountUser>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AccountUser>>([]);
        public Task<AccountUser?> FindManagedAsync(string subject, CancellationToken ct) => Task.FromResult<AccountUser?>(null);
        public Task<AccountClaims?> GetClaimsAsync(string subject, CancellationToken ct) => Task.FromResult<AccountClaims?>(subject switch
        {
            _ when subject == Hermano => new AccountClaims(Hermano, "Hermano Ficticio", "member", true, [InstitutionalRoles.TallerTesoreria], null, []),
            _ when subject == Admin => new AccountClaims(Admin, "Admin Ficticio", "member", true, [InstitutionalRoles.GranLogiaAdmin], "order", []),
            _ when subject == Superusuario => new AccountClaims(Superusuario, "Soporte Ficticio", "superuser", true, [], "order", []),
            _ when subject == OtroSuper => new AccountClaims(OtroSuper, "Otro Soporte", "superuser", true, [], "order", []),
            _ => null
        });
        public Task SendPasswordRecoveryEmailAsync(string subject, CancellationToken ct) => throw new NotSupportedException();
        public Task SetTemporaryPasswordAsync(string subject, string password, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Name = "SupportCorrectionSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var who = Request.Headers["X-Who"].FirstOrDefault() ?? "other";
            var (sub, name, roles) = who switch
            {
                "master" => (MasterSub, "Master Ficticio", new[] { InstitutionalRoles.PlatformSuperAdmin }),
                "super" => (Superusuario, "Soporte Ficticio", new[] { InstitutionalRoles.PlatformSuperAdmin }),
                "brother" => (Hermano, "Hermano Ficticio", Array.Empty<string>()),
                _ => (OtroSuper, "Otro Soporte", new[] { InstitutionalRoles.PlatformSuperAdmin }),
            };
            var claims = new List<Claim> { new("iss", Issuer), new("sub", sub), new("name", name), new("preferred_username", who == "master" ? "admin" : name) };
            if (roles.Length > 0) claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
            claims.AddRange(roles.Select(r => new Claim(InstitutionalClaims.Role, r)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Name)), Name)));
        }
    }
}
