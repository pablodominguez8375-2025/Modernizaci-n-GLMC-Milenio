using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using Npgsql;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyRosterListingHttpTests
{
    [Fact]
    public async Task Listings_mask_rut_and_only_internal_affairs_can_lift_the_debt_block()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<RegimenInteriorDbContext>();
                s.RemoveAll<DbContextOptions<RegimenInteriorDbContext>>();
                s.AddDbContext<RegimenInteriorDbContext>(o => o.UseNpgsql(cs));
                s.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = ListingTestAuth.SchemeName;
                    o.DefaultChallengeScheme = ListingTestAuth.SchemeName;
                    o.DefaultForbidScheme = ListingTestAuth.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, ListingTestAuth>(
                    ListingTestAuth.SchemeName, _ => {});
            }));

        var assembly = new AssemblyRecord
        {
            Name = "Asamblea de listados",
            MeetingDate = new DateOnly(2026, 11, 19),
            CutoffDate = new DateOnly(2026, 10, 19),
            CreatedBySubject = "qa-interno"
        };
        var orgId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>()
                .Database.MigrateAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Assemblies.Add(assembly);
            var member = new AssemblyMemberStatusRecord
            {
                AssemblyId = assembly.Id,
                MemberId = Guid.NewGuid(), PersonId = Guid.NewGuid(),
                OrganizationId = orgId,
                Category = "representative",
                SourceOfficeAssignmentId = Guid.NewGuid(),
                SourceEvidenceReference = "VM-TEST",
                FirstNamesAtCutoff = "Nombre",
                LastNamesAtCutoff = "Apellido",
                RutAtCutoff = "12345678-5",
                WorkshopAtCutoff = "Taller moroso sintético",
                VerifiedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            };
            db.AssemblyMemberStatuses.Add(member);
            db.AssemblyEligibilities.Add(new AssemblyEligibilityRecord
            {
                AssemblyId = assembly.Id, MemberStatusId = member.Id,
                Status = AssemblyVoterEligibilityPolicy.AttendanceOnly,
                MayAttend = true, MayVote = false,
                ReasonCode = AssemblyRosterListingBuilder.DebtReasonCode,
                EvaluatedBySubject = "qa-interno"
            });
            await db.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        var listUrl = $"/api/regimen-interior/asambleas/{assembly.Id}/listados";
        var lockUrl = $"/api/regimen-interior/asambleas/{assembly.Id}/talleres/{orgId}/bloqueo-saldo";

        var first = await client.GetAsync(listUrl, ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var text = await first.Content.ReadAsStringAsync(ct);
        Assert.Contains("12.345.***-5", text);
        Assert.DoesNotContain("12345678", text);
        Assert.DoesNotContain("12.345.678", text);
        Assert.Contains("\"sufragantes\":[]", text);

        var body = new { action = "lift", reason = "Convenio de pago acreditado", evidenceReference = "ACTA-RI-1" };

        // Gran Secretaría puede leer, pero no levantar el bloqueo.
        client.DefaultRequestHeaders.Add("X-Assembly-Test-Role", InstitutionalRoles.GranSecretaria);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(listUrl, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(lockUrl, body, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Assembly-Test-Role");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(lockUrl,
            new { action = "lift", reason = "corto", evidenceReference = "x" }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(lockUrl, body, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(lockUrl, body, ct)).StatusCode);

        var lifted = await (await client.GetAsync(listUrl, ct)).Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("\"sufragantes\":[]", lifted);

        var revoke = new { action = "revoke", reason = "Convenio incumplido por el Taller", evidenceReference = "ACTA-RI-2" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(lockUrl, revoke, ct)).StatusCode);
        var again = await (await client.GetAsync(listUrl, ct)).Content.ReadAsStringAsync(ct);
        Assert.Contains("\"sufragantes\":[]", again);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/regimen-interior/asambleas/{assembly.Id}/talleres/{Guid.NewGuid()}/bloqueo-saldo", body, ct)).StatusCode);
    }

    [Fact]
    public async Task Only_internal_affairs_records_attestations_and_the_readiness_report_reflects_them()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<RegimenInteriorDbContext>();
                s.RemoveAll<DbContextOptions<RegimenInteriorDbContext>>();
                s.AddDbContext<RegimenInteriorDbContext>(o => o.UseNpgsql(cs));
                s.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = ListingTestAuth.SchemeName;
                    o.DefaultChallengeScheme = ListingTestAuth.SchemeName;
                    o.DefaultForbidScheme = ListingTestAuth.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, ListingTestAuth>(
                    ListingTestAuth.SchemeName, _ => {});
            }));

        var empty = new AssemblyRecord { Name = "Asamblea sin asambleístas", MeetingDate = new(2026, 11, 19), CutoffDate = new(2026, 10, 19), CreatedBySubject = "qa-interno" };
        var assembly = new AssemblyRecord { Name = "Asamblea de constancias", MeetingDate = new(2026, 11, 19), CutoffDate = new(2026, 10, 19), CreatedBySubject = "qa-interno" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Database.MigrateAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Assemblies.AddRange(empty, assembly);
            db.AssemblyMemberStatuses.Add(new AssemblyMemberStatusRecord
            {
                AssemblyId = assembly.Id, MemberId = Guid.NewGuid(), PersonId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(),
                Category = "representative", SourceOfficeAssignmentId = Guid.NewGuid(), SourceEvidenceReference = "VM-TEST",
                FirstNamesAtCutoff = "Nombre", LastNamesAtCutoff = "Apellido", RutAtCutoff = "12345678-5",
                WorkshopAtCutoff = "Taller sintético", VerifiedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            });
            await db.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        var url = $"/api/regimen-interior/asambleas/{assembly.Id}/constancias";
        var good = new { kind = "honor_coverage", action = "attest", actaReference = "ACTA-RI-7", statement = "El Tribunal de Honor informó cobertura completa al corte." };

        client.DefaultRequestHeaders.Add("X-Assembly-Test-Role", InstitutionalRoles.GranSecretaria);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(url, good, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Assembly-Test-Role");

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { kind = "honor_coverage", action = "attest", actaReference = "", statement = "Declaración suficiente." }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { kind = "tesoreria", action = "attest", actaReference = "A", statement = "Declaración suficiente." }, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/regimen-interior/asambleas/{empty.Id}/constancias", good, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(url, new { kind = "honor_coverage", action = "revoke", actaReference = "A", statement = "Declaración suficiente." }, ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url, good, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(url, good, ct)).StatusCode);
        var listed = await (await client.GetAsync(url, ct)).Content.ReadAsStringAsync(ct);
        Assert.Contains("ACTA-RI-7", listed);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url, new { kind = "honor_coverage", action = "revoke", actaReference = "ACTA-RI-8", statement = "Se retira por corrección del acta." }, ct)).StatusCode);
        var after = await (await client.GetAsync(url, ct)).Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("ACTA-RI-7", after);

        var readiness = await (await client.GetAsync($"/api/regimen-interior/asambleas/{assembly.Id}/cierre/estado", ct)).Content.ReadAsStringAsync(ct);
        Assert.Contains("\"mayClose\":false", readiness);
    }

    [Fact]
    public async Task Full_closure_flow_accredits_evaluates_closes_once_and_freezes_the_assembly()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(cs)
            .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<RegimenInteriorDbContext>();
                s.RemoveAll<DbContextOptions<RegimenInteriorDbContext>>();
                s.AddDbContext<RegimenInteriorDbContext>(o => o.UseNpgsql(cs));
                s.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = ListingTestAuth.SchemeName;
                    o.DefaultChallengeScheme = ListingTestAuth.SchemeName;
                    o.DefaultForbidScheme = ListingTestAuth.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, ListingTestAuth>(
                    ListingTestAuth.SchemeName, _ => {});
            }));

        var assembly = new AssemblyRecord { Name = "Asamblea de cierre", MeetingDate = new(2026, 11, 19), CutoffDate = new(2026, 10, 19), CreatedBySubject = "qa-interno" };
        var workshopX = new Organization { Name = "Taller X sintético", Type = "workshop" };
        var workshopY = new Organization { Name = "Taller Y sintético", Type = "workshop" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var app = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await app.Database.MigrateAsync(ct);
            // Registro canónico del cargo: solo debe existir el de esta prueba.
            app.OfficeAssignments.RemoveRange(app.OfficeAssignments.Where(o => o.OfficeType == "venerable_master" || o.OfficeType == "worshipful_master"));
            await app.SaveChangesAsync(ct);
            (string last, Organization org, DateOnly? end)[] people =
            [("Álvarez", workshopX, new DateOnly(2021, 12, 31)), ("Bravo", workshopX, null), ("Castro", workshopY, new DateOnly(2022, 12, 31))];
            app.AddRange(workshopX, workshopY);
            foreach (var (last, org, end) in people)
            {
                var person = new Person { FirstNames = "Nombre", LastNames = last, Rut = "9" + Random.Shared.Next(100000, 999999) + "-" + last.Length };
                var member = new Member { Person = person, PersonId = person.Id };
                app.AddRange(person, member,
                    new Membership { MemberId = member.Id, OrganizationId = org.Id, Status = "active", MembershipType = "regular", StartDate = new DateOnly(2015, 1, 1) },
                    new OfficeAssignment { MemberId = member.Id, OrganizationId = org.Id, OfficeType = "venerable_master", Period = "sintético",
                        StartDate = end is null ? new DateOnly(2026, 1, 1) : new DateOnly(2020, 1, 1), EndDate = end, EvidenceReference = "ACTA-VM-" + last });
            }
            await app.SaveChangesAsync(ct);
            var db = scope.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
            await db.Database.MigrateAsync(ct);
            db.Assemblies.Add(assembly);
            await db.SaveChangesAsync(ct);
        }

        using var client = factory.CreateClient();
        var root = $"/api/regimen-interior/asambleas/{assembly.Id}";
        async Task<HttpStatusCode> Post(string path, object body) => (await client.PostAsJsonAsync(root + path, body, ct)).StatusCode;
        var close = new { rosterReference = "ACTA-RI-CIERRE-1" };

        // Sin constancias nada avanza.
        Assert.Equal(HttpStatusCode.Conflict, await Post("/acreditar", new { }));
        Task<HttpStatusCode> Attest(string kind) => Post("/constancias", new { kind, action = "attest", actaReference = "ACTA-" + kind, statement = "Fuente verificada y completa al corte." });
        // Honor y Régimen Interior se certifican sobre asambleístas ya acreditados.
        Assert.Equal(HttpStatusCode.Conflict, await Attest("honor_coverage"));
        Assert.Equal(HttpStatusCode.OK, await Attest("master_periods"));
        Assert.Equal(HttpStatusCode.OK, await Attest("membership"));
        var accredit = await client.PostAsJsonAsync(root + "/acreditar", new { }, ct);
        Assert.Equal(HttpStatusCode.OK, accredit.StatusCode);
        Assert.Contains("\"accredited\":3", await accredit.Content.ReadAsStringAsync(ct));
        Assert.Equal(HttpStatusCode.Conflict, await Post("/acreditar", new { }));
        Assert.Equal(HttpStatusCode.OK, await Attest("honor_coverage"));
        Assert.Equal(HttpStatusCode.OK, await Attest("regimen_coverage"));

        // Sin saldos de Gran Tesorería no se puede cerrar, y el rechazo no deja rastro.
        Assert.Equal(HttpStatusCode.Conflict, await Post("/cerrar", close));
        // Los pagos y saldos solo los valida su responsable (Gran Tesorero o Gran Hospitalario), no Régimen Interior.
        var balances = new { sourceReference = "OFICIO-GT-1", currency = "CLP",
            items = new[] { new { organizationId = workshopX.Id, amountPending = 0m }, new { organizationId = workshopY.Id, amountPending = 1500m } } };
        Assert.Equal(HttpStatusCode.Forbidden, await Post("/saldos", balances));
        client.DefaultRequestHeaders.Add("X-Assembly-Test-Role", InstitutionalRoles.GranTesoreria);
        Assert.Equal(HttpStatusCode.BadRequest, await Post("/saldos", new { sourceReference = "OFICIO-GT-1", currency = "CLP", items = new[] { new { organizationId = workshopX.Id, amountPending = -1 } } }));
        Assert.Equal(HttpStatusCode.OK, await Post("/saldos", balances));
        var readBalances = await client.GetAsync("/api/regimen-interior/saldos-padron", ct);
        Assert.Equal(HttpStatusCode.OK, readBalances.StatusCode);
        var balanceBody = await readBalances.Content.ReadAsStringAsync(ct); Assert.True(balanceBody.Contains("1500"), balanceBody);
        // Con solo Gran Tesorería el saldo no está completo: falta lo que valida Gran Hospitalaria.
        client.DefaultRequestHeaders.Remove("X-Assembly-Test-Role");
        Assert.Equal(HttpStatusCode.Conflict, await Post("/cerrar", close));
        client.DefaultRequestHeaders.Add("X-Assembly-Test-Role", InstitutionalRoles.GranHospitalaria);
        Assert.Equal(HttpStatusCode.OK, await Post("/saldos", new { sourceReference = "REG-GH-1", currency = "CLP",
            items = new[] { new { organizationId = workshopX.Id, amountPending = 0m }, new { organizationId = workshopY.Id, amountPending = 0m } } }));
        client.DefaultRequestHeaders.Remove("X-Assembly-Test-Role");

        var evaluated = await client.PostAsJsonAsync(root + "/evaluar", new { }, ct);
        Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        var text = await evaluated.Content.ReadAsStringAsync(ct);
        Assert.Contains("\"habilitado\":2", text);
        Assert.Contains("\"solo_asistencia\":1", text);

        // Solo Régimen Interior cierra.
        client.DefaultRequestHeaders.Add("X-Assembly-Test-Role", InstitutionalRoles.GranSecretaria);
        Assert.Equal(HttpStatusCode.Forbidden, await Post("/cerrar", close));
        client.DefaultRequestHeaders.Remove("X-Assembly-Test-Role");
        Assert.Equal(HttpStatusCode.BadRequest, await Post("/cerrar", new { rosterReference = "" }));

        // Régimen Interior levanta el bloqueo del Taller Y; el cierre re-evalúa y habilita a los 3.
        Assert.Equal(HttpStatusCode.OK, await Post($"/talleres/{workshopY.Id}/bloqueo-saldo", new { action = "lift", reason = "Convenio de pago acreditado", evidenceReference = "ACTA-RI-LEVANTA" }));
        var readiness = await (await client.GetAsync(root + "/cierre/estado", ct)).Content.ReadAsStringAsync(ct);
        Assert.Contains("\"mayClose\":true", readiness);
        // El padrón oficial NO se descarga mientras la Asamblea esté en borrador.
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(root + "/padron-oficial?listado=sufragantes&formato=pdf", ct)).StatusCode);
        var closed = await client.PostAsJsonAsync(root + "/cerrar", close, ct);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var closedText = await closed.Content.ReadAsStringAsync(ct);
        Assert.Contains("\"voters\":3", closedText);
        Assert.Matches("\"digest\":\"[0-9a-f]{64}\"", closedText);

        // Una Asamblea cerrada no admite más cambios ni segundo cierre, por la API ni por la base.
        Assert.Equal(HttpStatusCode.Conflict, await Post("/cerrar", close));
        Assert.Equal(HttpStatusCode.Conflict, await Post("/evaluar", new { }));
        Assert.Equal(HttpStatusCode.Conflict, await Post("/constancias", new { kind = "membership", action = "revoke", actaReference = "X", statement = "Intento tardío de retiro." }));
        var listing = await (await client.GetAsync(root + "/listados", ct)).Content.ReadAsStringAsync(ct);
        Assert.Contains("\"officiallyClosed\":true", listing);
        Assert.Contains("\"provisional\":false", listing);
        Assert.Contains("\"mayVote\":true", listing);
        Assert.DoesNotContain("ELIGIBILITY_SOURCES_UNVERIFIED", listing); // el Taller con bloqueo levantado también sufraga en el padrón cerrado
        // Padrón oficial: PDF y CSV de ambos listados, solo con la Asamblea cerrada; RUT enmascarado; firma en blanco.
        var pdf = await client.GetAsync(root + "/padron-oficial?listado=sufragantes&formato=pdf", ct);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync(ct))[..5]));
        Assert.Equal("true", pdf.Headers.GetValues("X-PMGM-Official-Roster").Single());
        Assert.Matches("^[0-9a-f]{64}$", pdf.Headers.GetValues("X-PMGM-Closure-Digest").Single());
        var csvAll = await (await client.GetAsync(root + "/padron-oficial?listado=asambleistas&formato=csv", ct)).Content.ReadAsStringAsync(ct);
        Assert.Contains("Álvarez, Nombre", csvAll);
        Assert.DoesNotContain("***", csvAll); // el padrón oficial impreso lleva el RUT completo
        Assert.Matches("\"9\\d{6}-\\d\"", csvAll);
        Assert.Equal(4, csvAll.Trim().Split("\r\n").Length); // encabezado + 3 asambleístas
        Assert.Contains("Calidad", csvAll); // el listado de asambleístas indica la calidad, no el sufragio
        Assert.DoesNotContain("Estado de sufragio", csvAll);
        Assert.Contains("Asambleísta", csvAll);
        Assert.Contains("Representante", csvAll);
        var csvVoters = await (await client.GetAsync(root + "/padron-oficial?listado=sufragantes&formato=csv", ct)).Content.ReadAsStringAsync(ct);
        Assert.Equal(4, csvVoters.Trim().Split("\r\n").Length); // el padrón de sufragio coincide con los 3 sufragantes del cierre
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(root + "/padron-oficial?listado=otro", ct)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Assembly-Test-Role", InstitutionalRoles.GranTesoreria);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(root + "/padron-oficial?listado=sufragantes&formato=csv", ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Assembly-Test-Role");
        await using var scope2 = factory.Services.CreateAsyncScope();
        var check = scope2.ServiceProvider.GetRequiredService<RegimenInteriorDbContext>();
        await Assert.ThrowsAsync<PostgresException>(() => check.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE core.assemblies SET \"Name\" = 'otro' WHERE \"Id\" = {assembly.Id}", ct));
        await Assert.ThrowsAsync<PostgresException>(() => check.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.assembly_eligibilities WHERE \"AssemblyId\" = {assembly.Id}", ct));
    }

    private sealed class ListingTestAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AssemblyListingSynthetic";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", "ri-test"),
                new Claim(InstitutionalClaims.Scope, "order"),
                new Claim(InstitutionalClaims.Role, Request.Headers["X-Assembly-Test-Role"].FirstOrDefault() ?? InstitutionalRoles.RegimenInterior)
            ], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
