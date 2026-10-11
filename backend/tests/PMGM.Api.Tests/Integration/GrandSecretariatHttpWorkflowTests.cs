using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.GrandSecretariat;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class GrandSecretariatHttpWorkflowTests
{
    [Fact]
    public async Task Authorized_ceremony_can_reserve_space_and_issue_formal_authorization()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new GrandSecretariatWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        Guid ceremonyId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Gran Secretaría CI {Guid.NewGuid():N}",
                Number = "GS-CI",
                Type = "workshop"
            };
            var ceremony = new CeremonyRequest
            {
                OrganizationId = organization.Id,
                Organization = organization,
                CeremonyType = CeremonyCodes.Type.WageIncrease,
                ProposedDate = new DateOnly(2026, 10, 15),
                Status = CeremonyCodes.RequestStatus.Authorized
            };

            db.AddRange(organization, ceremony);
            db.AuditEvents.Add(new AuditEvent
            {
                Action = "ceremony.authorization.approved",
                EntityType = nameof(CeremonyRequest),
                EntityId = ceremony.Id.ToString(),
                OrganizationId = organization.Id,
                Result = "success",
                CorrelationId = Guid.NewGuid().ToString("D"),
                ActorSubject = "ci-grand-secretariat-authorizer",
                MetadataJson = JsonSerializer.Serialize(new { evaluatedAsOf = "2026-10-11", approved = true })
            });
            await db.SaveChangesAsync(cancellationToken);
            organizationId = organization.Id;
            ceremonyId = ceremony.Id;
        }

        var createSpace = await client.PostAsJsonAsync(
            "/api/gran-secretaria/espacios",
            new
            {
                code = $"TEMP-{Guid.NewGuid():N}"[..20],
                name = "Templo CI",
                spaceType = GrandSecretariatCodes.SpaceType.Temple,
                location = "Casa Masónica CI",
                capacity = 80
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createSpace.StatusCode);

        var spaceJson = await createSpace.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var spaceId = spaceJson.GetProperty("id").GetGuid();
        var startsAtUtc = new DateTimeOffset(2026, 10, 15, 22, 0, 0, TimeSpan.Zero);
        var endsAtUtc = startsAtUtc.AddHours(3);

        var reservationResponse = await client.PostAsJsonAsync(
            "/api/gran-secretaria/reservas",
            new
            {
                spaceId,
                organizationId,
                ceremonyRequestId = ceremonyId,
                purpose = "Aumento de salario CI",
                startsAtUtc,
                endsAtUtc,
                notes = (string?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, reservationResponse.StatusCode);

        var reservationJson = await reservationResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var reservationId = reservationJson.GetProperty("id").GetGuid();

        var queueBeforeResponse = await client.GetAsync(
            "/api/institutional/gran-secretaria/ceremonias-autorizadas",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, queueBeforeResponse.StatusCode);
        var cacheControl = queueBeforeResponse.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl.NoStore);
        Assert.True(cacheControl.Private);

        var queueBeforeJson = await queueBeforeResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var queueBeforeItem = queueBeforeJson.GetProperty("items")
            .EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == ceremonyId);
        Assert.False(queueBeforeItem.GetProperty("formalAuthorizationIssued").GetBoolean());
        Assert.Equal(reservationId, queueBeforeItem.GetProperty("spaceReservationId").GetGuid());
        Assert.Equal("Templo CI", queueBeforeItem.GetProperty("spaceName").GetString());
        Assert.Equal(startsAtUtc, queueBeforeItem.GetProperty("reservationStartsAtUtc").GetDateTimeOffset());
        Assert.Equal(endsAtUtc, queueBeforeItem.GetProperty("reservationEndsAtUtc").GetDateTimeOffset());
        Assert.False(queueBeforeItem.TryGetProperty("memberId", out _));
        Assert.False(queueBeforeItem.TryGetProperty("candidatePersonId", out _));
        Assert.False(queueBeforeItem.TryGetProperty("notes", out _));

        var reservationsProjectionResponse = await client.GetAsync(
            $"/api/gran-secretaria/reservas?fromUtc={Uri.EscapeDataString(startsAtUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(endsAtUtc.ToString("O"))}&ceremonyRequestId={ceremonyId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reservationsProjectionResponse.StatusCode);
        var reservationsProjectionJson = await reservationsProjectionResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var reservationProjection = reservationsProjectionJson.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(reservationId, reservationProjection.GetProperty("id").GetGuid());
        Assert.Equal(ceremonyId, reservationProjection.GetProperty("ceremonyRequestId").GetGuid());
        Assert.False(reservationProjection.TryGetProperty("notes", out _));

        var overlapResponse = await client.PostAsJsonAsync(
            "/api/gran-secretaria/reservas",
            new
            {
                spaceId,
                organizationId,
                ceremonyRequestId = ceremonyId,
                purpose = "Reserva solapada CI",
                startsAtUtc = startsAtUtc.AddMinutes(30),
                endsAtUtc = endsAtUtc.AddMinutes(30),
                notes = (string?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, overlapResponse.StatusCode);

        var availabilityResponse = await client.GetAsync(
            $"/api/gran-secretaria/espacios/disponibilidad?fromUtc={Uri.EscapeDataString(startsAtUtc.ToString("O"))}&toUtc={Uri.EscapeDataString(endsAtUtc.ToString("O"))}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, availabilityResponse.StatusCode);
        var availabilityJson = await availabilityResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var targetSpace = availabilityJson.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == spaceId);
        Assert.False(targetSpace.GetProperty("isAvailable").GetBoolean());

        var authorizationResponse = await client.PostAsJsonAsync(
            $"/api/gran-secretaria/ceremonias/{ceremonyId}/autorizacion",
            new { spaceReservationId = reservationId },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, authorizationResponse.StatusCode);

        var authorizationJson = await authorizationResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(GrandSecretariatCodes.DocumentType.Plancha, authorizationJson.GetProperty("documentType").GetString());
        Assert.Equal(GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization, authorizationJson.GetProperty("planchaKind").GetString());
        Assert.Matches(@"^PLA-AUT-CER-\d{4}-\d{6}$",
            authorizationJson.GetProperty("documentCode").GetString()!);
        Assert.Contains("registro de auditoría", authorizationJson.GetProperty("content").GetString());
        Assert.Equal(ceremonyId, authorizationJson.GetProperty("relatedCeremonyRequestId").GetGuid());
        Assert.Equal(reservationId, authorizationJson.GetProperty("spaceReservationId").GetGuid());

        var queueAfterResponse = await client.GetAsync(
            "/api/institutional/gran-secretaria/ceremonias-autorizadas",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, queueAfterResponse.StatusCode);
        var queueAfterJson = await queueAfterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var queueAfterItem = queueAfterJson.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == ceremonyId);
        Assert.True(queueAfterItem.GetProperty("formalAuthorizationIssued").GetBoolean());
        Assert.Equal(reservationId, queueAfterItem.GetProperty("spaceReservationId").GetGuid());

        var lodgeAuthorizationOptions = await client.GetAsync(
            $"/api/secretaria/talleres/{organizationId}/autorizaciones-ceremonia",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, lodgeAuthorizationOptions.StatusCode);
        var lodgeAuthorizationOptionsJson = await lodgeAuthorizationOptions.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var lodgeAuthorizationOption = lodgeAuthorizationOptionsJson.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(authorizationJson.GetProperty("id").GetGuid(), lodgeAuthorizationOption.GetProperty("id").GetGuid());
        Assert.Equal(ceremonyId, lodgeAuthorizationOption.GetProperty("ceremonyRequestId").GetGuid());
        Assert.Equal(CeremonyCodes.Type.WageIncrease, lodgeAuthorizationOption.GetProperty("ceremonyType").GetString());
        Assert.StartsWith("PLA-AUT-CER-", lodgeAuthorizationOption.GetProperty("documentCode").GetString());

        var duplicateAuthorization = await client.PostAsJsonAsync(
            $"/api/gran-secretaria/ceremonias/{ceremonyId}/autorizacion",
            new { spaceReservationId = reservationId },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateAuthorization.StatusCode);

        var decreeResponse = await client.PostAsJsonAsync(
            "/api/gran-secretaria/documentos",
            new
            {
                documentType = GrandSecretariatCodes.DocumentType.Decree,
                planchaKind = (string?)null,
                title = "Decreto CI",
                content = "Contenido institucional de prueba para validar el circuito documental de Gran Secretaría.",
                organizationId = (Guid?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, decreeResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
            var authorization = await db.SecretariatDocuments
                .AsNoTracking()
                .SingleAsync(
                    x => x.RelatedCeremonyRequestId == ceremonyId &&
                         x.DocumentType == GrandSecretariatCodes.DocumentType.Plancha &&
                         x.PlanchaKind == GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization,
                    cancellationToken);

            Assert.Equal(GrandSecretariatCodes.DocumentStatus.Issued, authorization.Status);
            Assert.Contains(authorization.DocumentCode, authorization.Content);
            Assert.Equal(reservationId, authorization.SpaceReservationId);

            var auditActions = await db.AuditEvents
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId || x.OrganizationId == null)
                .Select(x => x.Action)
                .ToListAsync(cancellationToken);

            Assert.Contains("grand_secretariat.space.created", auditActions);
            Assert.Contains("grand_secretariat.space_reservation.created", auditActions);
            Assert.Contains("grand_secretariat.space_reservation.rejected", auditActions);
            Assert.Contains("grand_secretariat.ceremony_authorization.issued", auditActions);
            Assert.Contains("grand_secretariat.document.issued", auditActions);
        }
    }

    [Fact]
    public async Task Concurrent_issuance_is_unique_sequential_and_requires_authorization_audit()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new GrandSecretariatWebApplicationFactory(connectionString);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var org = new Organization
        {
            Name = $"CI Correlativo GS {Guid.NewGuid():N}",
            Type = "workshop"
        };
        var first = new CeremonyRequest
        {
            Organization = org,
            OrganizationId = org.Id,
            Status = CeremonyCodes.RequestStatus.Authorized,
            CeremonyType = CeremonyCodes.Type.Initiation
        };
        var second = new CeremonyRequest
        {
            Organization = org,
            OrganizationId = org.Id,
            Status = CeremonyCodes.RequestStatus.Authorized,
            CeremonyType = CeremonyCodes.Type.Exaltation
        };
        var withoutAudit = new CeremonyRequest
        {
            Organization = org,
            OrganizationId = org.Id,
            Status = CeremonyCodes.RequestStatus.Authorized,
            CeremonyType = CeremonyCodes.Type.WageIncrease
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            db.AddRange(org, first, second, withoutAudit);
            foreach (var authorized in new[] { first, second })
                db.AuditEvents.Add(new AuditEvent
                {
                    Action = "ceremony.authorization.approved",
                    EntityType = nameof(CeremonyRequest),
                    EntityId = authorized.Id.ToString(),
                    OrganizationId = org.Id,
                    Result = "success",
                    CorrelationId = Guid.NewGuid().ToString(),
                    MetadataJson = JsonSerializer.Serialize(new { evaluatedAsOf = "2026-10-11", approved = true })
                });
            await db.SaveChangesAsync(ct);
        }

        var unaudited = await firstClient.PostAsJsonAsync(
            $"/api/gran-secretaria/ceremonias/{withoutAudit.Id}/autorizacion",
            new { spaceReservationId = (Guid?)null }, ct);
        Assert.Equal(HttpStatusCode.Conflict, unaudited.StatusCode);

        var route = $"/api/gran-secretaria/ceremonias/{first.Id}/autorizacion";
        var concurrent = await Task.WhenAll(
            firstClient.PostAsJsonAsync(route, new { spaceReservationId = (Guid?)null }, ct),
            secondClient.PostAsJsonAsync(route, new { spaceReservationId = (Guid?)null }, ct));
        Assert.Equal(1, concurrent.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, concurrent.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        var created = await Assert.Single(concurrent, x => x.StatusCode == HttpStatusCode.Created)
            .Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var issuedFirstCode = created.GetProperty("documentCode").GetString()!;
        var issuedSecond = await firstClient.PostAsJsonAsync(
            $"/api/gran-secretaria/ceremonias/{second.Id}/autorizacion",
            new { spaceReservationId = (Guid?)null }, ct);
        Assert.Equal(HttpStatusCode.Created, issuedSecond.StatusCode);
        var issuedSecondCode = (await issuedSecond.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("documentCode").GetString()!;
        Assert.Equal(issuedFirstCode[..^6], issuedSecondCode[..^6]);
        Assert.Equal(int.Parse(issuedFirstCode[^6..]) + 1, int.Parse(issuedSecondCode[^6..]));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
            var rows = await db.SecretariatDocuments.AsNoTracking()
                .Where(x => x.RelatedCeremonyRequestId == first.Id ||
                            x.RelatedCeremonyRequestId == second.Id ||
                            x.RelatedCeremonyRequestId == withoutAudit.Id)
                .ToListAsync(ct);
            Assert.Equal(2, rows.Count);
            Assert.DoesNotContain(rows, x => x.RelatedCeremonyRequestId == withoutAudit.Id);
            Assert.All(rows, x => Assert.Matches(@"^PLA-AUT-CER-\d{4}-\d{6}$", x.DocumentCode));
            var audits = await db.AuditEvents.AsNoTracking()
                .Where(x => x.Action == "grand_secretariat.ceremony_authorization.issued" &&
                            x.OrganizationId == org.Id).ToListAsync(ct);
            Assert.Equal(2, audits.Count);
            Assert.All(audits, x => Assert.Contains("sourceAuthorizationSnapshotSha256", x.MetadataJson));
        }
    }

}

internal sealed class GrandSecretariatWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>();
            services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.RemoveAll<GrandSecretariatDbContext>();
            services.RemoveAll<DbContextOptions<GrandSecretariatDbContext>>();

            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<GrandSecretariatDbContext>(options => options.UseNpgsql(connectionString));

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }
}
