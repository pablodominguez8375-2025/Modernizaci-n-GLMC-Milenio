using PMGM.Api.Modules.GrandSecretariat.Entities;
using PMGM.Api.Modules.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Security.Claims;
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
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Notifications;
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

    [Fact]
    public async Task Two_lodges_are_isolated_and_grand_secretariat_alone_creates_reservation()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        using var factory = new SpaceRequestTestFactory(connectionString);
        var ct = TestContext.Current.CancellationToken;
        var lodgeA = Guid.NewGuid();
        var lodgeB = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var templeId = Guid.NewGuid();
        var ceremonyId = Guid.NewGuid();
        var grandRecipient = "qa-grand-space-" + Guid.NewGuid().ToString("N");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            // Notification migration is applied separately because it belongs
            // to NotificationDbContext, not PmgmDbContext.
            await using (var notificationDb = new NotificationDbContext(
                new DbContextOptionsBuilder<NotificationDbContext>()
                    .UseNpgsql(connectionString).Options))
                await notificationDb.Database.MigrateAsync(ct);
            db.Organizations.AddRange(
                new Organization { Id = lodgeA, Name = "Taller QA A", Number = "QA-A", Type = "workshop" },
                new Organization { Id = lodgeB, Name = "Taller QA B", Number = "QA-B", Type = "workshop" });
            db.CeremonyRequests.Add(new CeremonyRequest
            {
                Id = ceremonyId, OrganizationId = lodgeA,
                CeremonyType = CeremonyCodes.Type.WageIncrease,
                ProposedDate = new DateOnly(2035, 12, 5),
                Status = CeremonyCodes.RequestStatus.Authorized
            });
            await db.SaveChangesAsync(ct);

            // Configuración institucional explícita: un subject vinculado a
            // hermano activo con perfil Gran Secretaría de alcance Orden.
            var orderLodge = new Organization {
                Name = "Taller del receptor institucional " + Guid.NewGuid().ToString("N")[..8],
                Type = "workshop"
            };
            var personRecipient = new Person { FirstNames = "Gran", LastNames = "Secretaria Sintética" };
            var memberRecipient = new Member { Person = personRecipient };
            db.Organizations.Add(orderLodge);
            db.Members.Add(memberRecipient);
            db.Memberships.Add(new Membership {
                Member = memberRecipient, Organization = orderLodge,
                MembershipType = "regular", Status = MembershipCodes.MembershipStatus.Active,
                StartDate = new DateOnly(2020, 1, 1)
            });
            var catalog = await DynamicAccessEndpoints.LoadAsync(db, ct);
            catalog.Profiles.Add(new DynamicProfile(Guid.NewGuid(),
                SpaceRequestCreatedNotices.RecipientProfileCode, "Gran Secretaría",
                "order", false, true, ["order"],
                [new DynamicGrant("secretariat", ["view"])]));
            catalog.Assignments.Add(new DynamicAssignment(Guid.NewGuid(), grandRecipient,
                SpaceRequestCreatedNotices.RecipientProfileCode, null,
                new DateOnly(2020, 1, 1), null, true));
            db.DynamicAccessSnapshots.Add(new DynamicAccessSnapshot {
                Version = catalog.Version + 1,
                Payload = JsonSerializer.Serialize(catalog)
            });
            await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links
                    ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject")
                VALUES ({Guid.NewGuid()}, {memberRecipient.Id}, {"urn:qa-space-request"},
                        {grandRecipient}, {DateTimeOffset.UtcNow}, {"qa-space"})
                """, ct);
            var gs = scope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
            gs.InstitutionalSpaces.Add(new InstitutionalSpace
            {
                Id = spaceId, Code = "QA-ROOM-" + Guid.NewGuid().ToString("N")[..8],
                Name = "Sala de test aislada", SpaceType = "secretariat_room", Capacity = 40,
                Status = "active"
            });
            gs.InstitutionalSpaces.Add(new InstitutionalSpace
            {
                Id = templeId, Code = "QA-TEMP-" + Guid.NewGuid().ToString("N")[..8],
                Name = "Gran Templo QA (ficticio)", SpaceType = "temple", Capacity = 80,
                Status = "active"
            });
            await gs.SaveChangesAsync(ct);
        }

        using var a = Client(factory, InstitutionalRoles.TallerSecretaria, lodgeA);
        using var b = Client(factory, InstitutionalRoles.TallerSecretaria, lodgeB);
        using var grand = Client(factory, InstitutionalRoles.GranSecretaria, null);
        using var treasurer = Client(factory, InstitutionalRoles.TallerTesoreria, lodgeA);
        var start = new DateTimeOffset(2035, 11, 1, 19, 0, 0, TimeSpan.Zero);
        object body = new
        {
            spaceType = "secretariat_room", preferredSpaceId = spaceId,
            startsAtUtc = start, endsAtUtc = start.AddHours(3), purpose = "tenida",
            notes = "Actividad ficticia", expectedAttendees = 25,
            ceremonyRequestId = (Guid?)null, equipmentRequest = (string?)null,
            equipmentResponsible = (string?)null, vmApprovalReference = (string?)null
        };

        var unauthorized = await treasurer.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", body, ct);
        Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);

        var created = await a.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", body, ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var requestId = createdJson.GetProperty("id").GetGuid();
        await using (var notices = new NotificationDbContext(
            new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connectionString).Options))
        {
            var createdNotice = await notices.NotificationMessages.AsNoTracking()
                .Include(x => x.Deliveries)
                .SingleAsync(x => x.RelatedResourceType == "space_request" &&
                                  x.RelatedResourceId == requestId.ToString("D") &&
                                  x.TypeCode == SpaceRequestCreatedNotices.TemplateCode &&
                                  x.RecipientSubject == grandRecipient, ct);
            Assert.Equal(grandRecipient, createdNotice.RecipientSubject);
            Assert.Null(createdNotice.RecipientEmail);
            Assert.Equal("internal", createdNotice.Classification);
            Assert.DoesNotContain("Actividad ficticia", createdNotice.Body);
            Assert.DoesNotContain("Taller QA A", createdNotice.Body);
            Assert.Equal(NotificationCodes.Channel.Internal,
                Assert.Single(createdNotice.Deliveries).Channel);
        }
        Assert.Equal(lodgeA, createdJson.GetProperty("organizationId").GetGuid());
        Assert.Equal("pending", createdJson.GetProperty("status").GetString());

        var bList = await b.GetAsync("/api/secretaria/solicitudes-espacio", ct);
        Assert.Equal(HttpStatusCode.OK, bList.StatusCode);
        var bRows = await bList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(0, bRows.GetProperty("total").GetInt32());

        var bCancel = await b.PostAsync("/api/secretaria/solicitudes-espacio/" + requestId + "/cancelar", null, ct);
        Assert.Equal(HttpStatusCode.NotFound, bCancel.StatusCode);

        var aList = await a.GetAsync("/api/secretaria/solicitudes-espacio", ct);
        var aRows = await aList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(1, aRows.GetProperty("total").GetInt32());

        var before = await a.GetAsync("/api/secretaria/espacios/disponibilidad?fromUtc=2035-11-01T19:00:00Z&toUtc=2035-11-01T22:00:00Z", ct);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeJson = await before.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.True(beforeJson.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == spaceId).GetProperty("isAvailable").GetBoolean());

        var grandRows = await grand.GetAsync("/api/gran-secretaria/solicitudes-espacio?status=pending", ct);
        Assert.Equal(HttpStatusCode.OK, grandRows.StatusCode);
        var grandJson = await grandRows.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Contains(grandJson.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == requestId);

        var approved = await grand.PostAsJsonAsync("/api/gran-secretaria/solicitudes-espacio/" + requestId + "/aprobar",
            new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approvedJson = await approved.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("approved", approvedJson.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty, approvedJson.GetProperty("spaceReservationId").GetGuid());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var notices = new NotificationDbContext(
                new DbContextOptionsBuilder<NotificationDbContext>()
                    .UseNpgsql(connectionString).Options);
            await using (notices)
            {
                var recorded = await notices.NotificationMessages.AsNoTracking()
                    .Include(x => x.Deliveries)
                    .Where(x => x.RelatedResourceType == "space_request" &&
                                x.RelatedResourceId == requestId.ToString("D") &&
                                x.TypeCode == "grand_secretariat.space_request.approved")
                    .ToListAsync(ct);
                var message = Assert.Single(recorded);
                Assert.Equal("grand_secretariat.space_request.approved", message.TypeCode);
                Assert.Equal("synthetic-qa-" + InstitutionalRoles.TallerSecretaria + "-" + lodgeA.ToString("D"),
                    message.RecipientSubject);
                Assert.Equal("internal", message.Classification);
                Assert.DoesNotContain("Actividad ficticia", message.Body);
                Assert.DoesNotContain("Taller QA B", message.Body);
                var delivery = Assert.Single(message.Deliveries);
                Assert.Equal(NotificationCodes.Channel.Internal, delivery.Channel);
                Assert.Equal(NotificationCodes.DeliveryStatus.Delivered, delivery.Status);
            }
        }

        var duplicate = await grand.PostAsJsonAsync("/api/gran-secretaria/solicitudes-espacio/" + requestId + "/aprobar",
            new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var conflicting = await b.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", body, ct);
        Assert.Equal(HttpStatusCode.Created, conflicting.StatusCode);
        var conflictingId = (await conflicting.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("id").GetGuid();
        var conflictResult = await grand.PostAsJsonAsync(
            "/api/gran-secretaria/solicitudes-espacio/" + conflictingId + "/aprobar", new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, conflictResult.StatusCode);
        var bAfter = await b.GetAsync("/api/secretaria/solicitudes-espacio", ct);
        var bAfterJson = await bAfter.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Contains(bAfterJson.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == conflictingId &&
                 x.GetProperty("status").GetString() == "pending");

        var after = await a.GetAsync("/api/secretaria/espacios/disponibilidad?fromUtc=2035-11-01T19:00:00Z&toUtc=2035-11-01T22:00:00Z", ct);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var serialized = await after.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("Taller QA B", serialized);
        Assert.DoesNotContain("Actividad ficticia", serialized);
        Assert.False((await after.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == spaceId)
            .GetProperty("isAvailable").GetBoolean());

        var rejected = await grand.PostAsJsonAsync(
            "/api/gran-secretaria/solicitudes-espacio/" + conflictingId + "/rechazar",
            new { reason = "Horario ocupado; buscar nueva fecha" }, ct);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedJson = await rejected.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("rejected", rejectedJson.GetProperty("status").GetString());
        await using (var notices = new NotificationDbContext(
            new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(connectionString).Options))
        {
            var aApproved = await notices.NotificationMessages.AsNoTracking().CountAsync(x =>
                x.RelatedResourceType == "space_request" &&
                x.RelatedResourceId == requestId.ToString("D") &&
                x.TypeCode == "grand_secretariat.space_request.approved", ct);
            Assert.Equal(1, aApproved); // Duplicate approval did not send a second notice.
            var rejectedNotice = await notices.NotificationMessages.AsNoTracking()
                .SingleAsync(x => x.RelatedResourceType == "space_request" &&
                                  x.RelatedResourceId == conflictingId.ToString("D") &&
                                  x.TypeCode == "grand_secretariat.space_request.rejected", ct);
            Assert.Equal("grand_secretariat.space_request.rejected", rejectedNotice.TypeCode);
            Assert.Equal("synthetic-qa-" + InstitutionalRoles.TallerSecretaria + "-" + lodgeB.ToString("D"),
                rejectedNotice.RecipientSubject);
            Assert.DoesNotContain("Horario ocupado", rejectedNotice.Body);
            Assert.DoesNotContain("Taller QA A", rejectedNotice.Body);
        }

        var ceremonyBody = new
        {
            spaceType = "secretariat_room", preferredSpaceId = spaceId,
            startsAtUtc = start.AddMonths(1), endsAtUtc = start.AddMonths(1).AddHours(2),
            purpose = "ceremonia", ceremonyRequestId = ceremonyId,
            notes = "Solicitud ficticia sin Plancha",
            expectedAttendees = 20,
            equipmentRequest = (string?)null, equipmentResponsible = (string?)null,
            vmApprovalReference = (string?)null
        };
        var ceremonyCreated = await a.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", ceremonyBody, ct);
        Assert.Equal(HttpStatusCode.Created, ceremonyCreated.StatusCode);
        var ceremonySpaceId = (await ceremonyCreated.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("id").GetGuid();
        var blockedWithoutPlancha = await grand.PostAsJsonAsync(
            "/api/gran-secretaria/solicitudes-espacio/" + ceremonySpaceId + "/aprobar",
            new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, blockedWithoutPlancha.StatusCode);

        var cancellation = await a.PostAsync(
            "/api/secretaria/solicitudes-espacio/" + ceremonySpaceId + "/cancelar", null, ct);
        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);
        Assert.Equal("cancelled",
            (await cancellation.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
                .GetProperty("status").GetString());
        await using (var notices = new NotificationDbContext(
            new DbContextOptionsBuilder<NotificationDbContext>()
                .UseNpgsql(connectionString).Options))
        {
            Assert.False(await notices.NotificationMessages.AnyAsync(x =>
                x.RelatedResourceType == "space_request" &&
                x.RelatedResourceId == ceremonySpaceId.ToString("D"), ct));
        }

        var templeBody = new {
            spaceType = "temple", preferredSpaceId = templeId,
            startsAtUtc = start.AddMonths(2), endsAtUtc = start.AddMonths(2).AddHours(2),
            purpose = "tenida", notes = "Solicitud de templo ficticia",
            expectedAttendees = 45, ceremonyRequestId = (Guid?)null,
            equipmentRequest = "Amplificación",
            equipmentResponsible = "Responsable QA",
            vmApprovalReference = "Referencia QA no firmada"
        };
        var templeCreated = await a.PostAsJsonAsync("/api/secretaria/solicitudes-espacio", templeBody, ct);
        Assert.Equal(HttpStatusCode.Created, templeCreated.StatusCode);
        var templeRequestId = (await templeCreated.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("id").GetGuid();
        var templeBlocked = await grand.PostAsJsonAsync(
            "/api/gran-secretaria/solicitudes-espacio/" + templeRequestId + "/aprobar",
            new { spaceId = templeId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, templeBlocked.StatusCode);

        // La ceremonia sin expediente es inválida incluso si el usuario omite ceremonyRequestId.
        var missingCeremonyId = await a.PostAsJsonAsync("/api/secretaria/solicitudes-espacio",
            new {
                spaceType = "secretariat_room", preferredSpaceId = spaceId,
                startsAtUtc = start.AddMonths(3), endsAtUtc = start.AddMonths(3).AddHours(2),
                purpose = "ceremonia", ceremonyRequestId = (Guid?)null,
                notes = (string?)null, expectedAttendees = 20,
                equipmentRequest = (string?)null, equipmentResponsible = (string?)null,
                vmApprovalReference = (string?)null
            }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingCeremonyId.StatusCode);

        // Una solicitud aún pending puede quedar con su fecha vencida; jamás confirmar en retrospectiva.
        var futurePending = await a.PostAsJsonAsync("/api/secretaria/solicitudes-espacio",
            new {
                spaceType = "secretariat_room", preferredSpaceId = spaceId,
                startsAtUtc = start.AddMonths(4), endsAtUtc = start.AddMonths(4).AddHours(2),
                purpose = "tenida", ceremonyRequestId = (Guid?)null,
                notes = (string?)null, expectedAttendees = 25,
                equipmentRequest = (string?)null, equipmentResponsible = (string?)null,
                vmApprovalReference = (string?)null
            }, ct);
        Assert.Equal(HttpStatusCode.Created, futurePending.StatusCode);
        var staleId = (await futurePending.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct))
            .GetProperty("id").GetGuid();
        await using (var staleScope = factory.Services.CreateAsyncScope())
        {
            var updateDb = staleScope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
            var stale = await updateDb.SpaceRequests.SingleAsync(x => x.Id == staleId, ct);
            stale.StartsAtUtc = DateTimeOffset.UtcNow.AddHours(-3);
            stale.EndsAtUtc = DateTimeOffset.UtcNow.AddHours(-1);
            await updateDb.SaveChangesAsync(ct);
        }
        var staleResponse = await grand.PostAsJsonAsync(
            "/api/gran-secretaria/solicitudes-espacio/" + staleId + "/aprobar",
            new { spaceId }, ct);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        await using var checkScope = factory.Services.CreateAsyncScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
        Assert.Equal(1, await checkDb.SpaceReservations.AsNoTracking()
            .CountAsync(x => x.SpaceId == spaceId && x.Status == "reserved", ct));
        Assert.True(await checkDb.AuditEvents.AsNoTracking().AnyAsync(x =>
            x.Action == "grand_secretariat.space_request.approved" &&
            x.EntityId == requestId.ToString(), ct));
    }

    [Fact]
    public async Task New_request_without_verified_grand_secretariat_recipient_rolls_back_request_and_audit()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new SpaceRequestTestFactory(connectionString);
        var lodgeId = Guid.NewGuid();
        DynamicAccessCatalog previous;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var core = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await core.Database.MigrateAsync(ct);
            var notifications = new NotificationDbContext(
                new DbContextOptionsBuilder<NotificationDbContext>()
                    .UseNpgsql(connectionString).Options);
            await using (notifications)
                await notifications.Database.MigrateAsync(ct);

            core.Organizations.Add(new Organization {
                Id = lodgeId, Name = "Taller de prueba sin receptor " + Guid.NewGuid().ToString("N")[..8],
                Type = "workshop"
            });
            previous = await DynamicAccessEndpoints.LoadAsync(core, ct);
            var disabled = previous with {
                Version = previous.Version + 1,
                Profiles = previous.Profiles
                    .Where(x => x.Code != SpaceRequestCreatedNotices.RecipientProfileCode).ToList(),
                Assignments = previous.Assignments
                    .Where(x => x.ProfileCode != SpaceRequestCreatedNotices.RecipientProfileCode).ToList()
            };
            core.DynamicAccessSnapshots.Add(new DynamicAccessSnapshot {
                Version = disabled.Version, Payload = JsonSerializer.Serialize(disabled)
            });
            await core.SaveChangesAsync(ct);
        }

        try
        {
            using var client = Client(factory, InstitutionalRoles.TallerSecretaria, lodgeId);
            var starts = DateTimeOffset.UtcNow.AddDays(20);
            var body = new {
                spaceType = "temple", preferredSpaceId = (Guid?)null,
                startsAtUtc = starts, endsAtUtc = starts.AddHours(2),
                purpose = "tenida", ceremonyRequestId = (Guid?)null,
                notes = "Dato reservado que NO debe quedar guardado",
                expectedAttendees = 20, equipmentRequest = (string?)null,
                equipmentResponsible = (string?)null, vmApprovalReference = (string?)null
            };
            using var response = await client.PostAsJsonAsync(
                "/api/secretaria/solicitudes-espacio", body, ct);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal("GRAND_SECRETARIAT_RECIPIENT_UNAVAILABLE",
                payload.GetProperty("code").GetString());

            await using var checkScope = factory.Services.CreateAsyncScope();
            var grand = checkScope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
            Assert.False(await grand.SpaceRequests.AsNoTracking()
                .AnyAsync(x => x.OrganizationId == lodgeId, ct));
            Assert.False(await grand.AuditEvents.AsNoTracking()
                .AnyAsync(x => x.Action == "grand_secretariat.space_request.created" &&
                    x.OrganizationId == lodgeId, ct));
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var core = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            // Restaurar la política original sin dejar configuración sintética
            // del test afectando las demás pruebas PostgreSQL del shard.
            var current = await DynamicAccessEndpoints.LoadAsync(core, ct);
            var restored = previous with { Version = current.Version + 1 };
            core.DynamicAccessSnapshots.Add(new DynamicAccessSnapshot {
                Version = restored.Version, Payload = JsonSerializer.Serialize(restored)
            });
            await core.SaveChangesAsync(ct);
        }
    }

    private static HttpClient Client(SpaceRequestTestFactory factory, string role, Guid? organizationId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Synthetic-Role", role);
        if (organizationId is not null)
            client.DefaultRequestHeaders.Add("X-Synthetic-Lodge", organizationId.Value.ToString());
        return client;
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

internal sealed class SpaceRequestTestFactory(string connectionString) : WebApplicationFactory<Program>
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
                options.DefaultAuthenticateScheme = SyntheticHeaderAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = SyntheticHeaderAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = SyntheticHeaderAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, SyntheticHeaderAuthenticationHandler>(
                SyntheticHeaderAuthenticationHandler.SchemeName, _ => { });
        });
    }
}

/// <summary>Only in integration test assembly. Never use these headers in production authentication.</summary>
public sealed class SyntheticHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "SyntheticSpaceRequestTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Synthetic-Role"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var org = Request.Headers["X-Synthetic-Lodge"].FirstOrDefault();
        var claims = new List<Claim> { new(InstitutionalClaims.Role, role),
            new("sub", "synthetic-qa-" + role + "-" + (org ?? "order")) };
        if (!string.IsNullOrWhiteSpace(org))
            claims.Add(new Claim(InstitutionalClaims.Organization, org));
        if (role == InstitutionalRoles.GranSecretaria)
            claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
