using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class PostgreSqlHttpWorkflowTests
{
    [Fact]
    public async Task WageIncrease_full_http_workflow_blocks_authorization_without_advancement_evidence_and_audits()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        Guid memberId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller HTTP CI {Guid.NewGuid():N}",
                Number = "HTTP-CI",
                Type = "workshop", City = "Santiago", Country = "Chile", OrienteCode = "santiago"
            };
            var person = new Person
            {
                FirstNames = "Hermano",
                LastNames = "Integración"
            };
            var member = new Member
            {
                PersonId = person.Id,
                Person = person,
                InstitutionalNumber = $"CI-{Guid.NewGuid():N}"
            };
            var membership = new Membership
            {
                MemberId = member.Id,
                Member = member,
                OrganizationId = organization.Id,
                Organization = organization,
                MembershipType = "regular",
                StartDate = new DateOnly(2026, 1, 1),
                Status = MembershipCodes.MembershipStatus.Active
            };

            db.AddRange(organization, person, member, membership);
            await db.SaveChangesAsync(cancellationToken);

            organizationId = organization.Id;
            memberId = member.Id;
        }

        var asOf = new DateOnly(2026, 9, 7);

        var treasuryResponse = await client.PostAsJsonAsync(
            $"/api/tesoreria/talleres/{organizationId}/regularidad",
            new
            {
                status = TreasuryCodes.RegularityStatus.UpToDate,
                asOfDate = asOf,
                sourceReference = "CI-HTTP-TREASURY",
                notes = (string?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, treasuryResponse.StatusCode);

        var hospitalariaResponse = await client.PostAsJsonAsync(
            $"/api/hospitalaria/talleres/{organizationId}/regularidad",
            new
            {
                status = HospitalariaCodes.RegularityStatus.UpToDate,
                asOfDate = asOf,
                sourceReference = "CI-HTTP-HOSPITALARIA",
                notes = (string?)null
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, hospitalariaResponse.StatusCode);

        var ceremonyResponse = await client.PostAsJsonAsync(
            "/api/ceremonias/solicitudes",
            new
            {
                organizationId,
                ceremonyType = CeremonyCodes.Type.WageIncrease,
                memberId,
                candidatePersonId = (Guid?)null,
                proposedDate = new DateOnly(2026, 10, 1),
                notes = "Nota restringida que nunca debe salir en la bandeja."
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, ceremonyResponse.StatusCode);

        var ceremonyJson = await ceremonyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var ceremonyId = ceremonyJson.GetProperty("id").GetGuid();

        var rightPaymentRequest = new
        {
            amount = 11000m,
            paymentMethod = "transfer",
            paymentDate = new DateOnly(2026, 9, 7),
            reference = "CI-HTTP-CEREMONY-RIGHT",
            idempotencyKey = $"ceremony-right-{Guid.NewGuid():N}"
        };
        var rightPaymentPath = $"/api/ceremonias/solicitudes/{ceremonyId}/derecho/pagos";
        var rightPaymentResponse = await client.PostAsJsonAsync(rightPaymentPath, rightPaymentRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, rightPaymentResponse.StatusCode);
        var rightPaymentJson = await rightPaymentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal("CLP", rightPaymentJson.GetProperty("currency").GetString());
        Assert.Equal(20000m, rightPaymentJson.GetProperty("balance").GetDecimal());
        var originalReceipt = rightPaymentJson.GetProperty("receiptNumber").GetString();

        var rightPaymentRetry = await client.PostAsJsonAsync(rightPaymentPath, rightPaymentRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, rightPaymentRetry.StatusCode);
        var retryJson = await rightPaymentRetry.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(originalReceipt, retryJson.GetProperty("receiptNumber").GetString());
        Assert.True(retryJson.GetProperty("replayed").GetBoolean());
        var treasuryRightsResponse = await client.GetAsync("/api/tesoreria/derechos-ceremoniales", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, treasuryRightsResponse.StatusCode);
        var treasuryRightsJson = await treasuryRightsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var treasuryRight = treasuryRightsJson.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == ceremonyId);
        Assert.Equal(11000m, treasuryRight.GetProperty("paid").GetDecimal());
        Assert.Equal(20000m, treasuryRight.GetProperty("balance").GetDecimal());
        Assert.False(treasuryRight.TryGetProperty("notes", out _));
        var duplicateReference = await client.PostAsJsonAsync(rightPaymentPath, new
        {
            amount = 11000m, rightPaymentRequest.paymentMethod, rightPaymentRequest.paymentDate,
            rightPaymentRequest.reference, idempotencyKey = $"duplicate-reference-{Guid.NewGuid():N}"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateReference.StatusCode);
        var changedRetry = await client.PostAsJsonAsync(rightPaymentPath, new
        {
            amount = 12000m, rightPaymentRequest.paymentMethod, rightPaymentRequest.paymentDate,
            rightPaymentRequest.reference, rightPaymentRequest.idempotencyKey
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, changedRetry.StatusCode);

        var overpayment = await client.PostAsJsonAsync(rightPaymentPath, new
        {
            amount = 20001m, rightPaymentRequest.paymentMethod, rightPaymentRequest.paymentDate,
            rightPaymentRequest.reference, idempotencyKey = $"overpay-{Guid.NewGuid():N}"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, overpayment.StatusCode);
        var remainingPayment = await client.PostAsJsonAsync(rightPaymentPath, new
        {
            amount = 20000m, rightPaymentRequest.paymentMethod, rightPaymentRequest.paymentDate,
            rightPaymentRequest.reference, idempotencyKey = $"finish-{Guid.NewGuid():N}"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, remainingPayment.StatusCode);
        var remainingJson = await remainingPayment.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(0m, remainingJson.GetProperty("balance").GetDecimal());

        var validationResponse = await client.PostAsJsonAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/validaciones/regimen-interior",
            new
            {
                status = CeremonyCodes.ValidationStatus.Approved,
                sourceReference = "CI-HTTP-RI",
                notes = "Observación interna restringida."
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, validationResponse.StatusCode);

        var eligibilityBeforeGrandMasterResponse = await client.GetAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/elegibilidad",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, eligibilityBeforeGrandMasterResponse.StatusCode);

        var eligibilityBeforeGrandMasterJson = await eligibilityBeforeGrandMasterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.False(eligibilityBeforeGrandMasterJson.GetProperty("canAuthorize").GetBoolean());

        var queueBeforeResponse = await client.GetAsync(
            "/api/institutional/ceremonias/bandeja",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, queueBeforeResponse.StatusCode);
        var cacheControl = queueBeforeResponse.Headers.CacheControl?.ToString() ?? string.Empty;
        Assert.Contains("private", cacheControl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", cacheControl, StringComparison.OrdinalIgnoreCase);

        var queueBeforeJson = await queueBeforeResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var queueBeforeItem = queueBeforeJson.GetProperty("items")
            .EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == ceremonyId);

        Assert.Equal(organizationId, queueBeforeItem.GetProperty("organizationId").GetGuid());
        Assert.Equal("Hermano Integración", queueBeforeItem.GetProperty("subjectDisplayName").GetString());
        Assert.Equal(CeremonyCodes.Type.WageIncrease, queueBeforeItem.GetProperty("ceremonyType").GetString());
        Assert.Equal(CeremonyCodes.RequestStatus.UnderReview, queueBeforeItem.GetProperty("status").GetString());
        Assert.False(queueBeforeItem.GetProperty("eligibility").GetProperty("canAuthorize").GetBoolean());
        Assert.True(queueBeforeItem.GetProperty("actions").GetProperty("canValidateInternalAffairs").GetBoolean());
        Assert.False(queueBeforeItem.GetProperty("actions").GetProperty("canAuthorize").GetBoolean());
        Assert.False(queueBeforeItem.GetProperty("actions").GetProperty("canPublishCandidate").GetBoolean());
        Assert.False(queueBeforeItem.TryGetProperty("memberId", out _));
        Assert.False(queueBeforeItem.TryGetProperty("candidatePersonId", out _));
        Assert.False(queueBeforeItem.TryGetProperty("notes", out _));
        Assert.False(queueBeforeItem.TryGetProperty("email", out _));
        Assert.False(queueBeforeItem.TryGetProperty("institutionalNumber", out _));

        var grandMasterResponse = await client.PostAsJsonAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/validaciones/gran-maestria",
            new
            {
                status = CeremonyCodes.ValidationStatus.Approved,
                sourceReference = "CI-HTTP-GM",
                notes = "Visto bueno institucional de integración."
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, grandMasterResponse.StatusCode);

        var eligibilityAfterGrandMasterResponse = await client.GetAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/elegibilidad",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, eligibilityAfterGrandMasterResponse.StatusCode);
        var eligibilityAfterGrandMasterJson = await eligibilityAfterGrandMasterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.False(eligibilityAfterGrandMasterJson.GetProperty("canAuthorize").GetBoolean());
        var advanceRequirement = eligibilityAfterGrandMasterJson.GetProperty("requirements").EnumerateArray()
            .Single(x => x.GetProperty("code").GetString() == CeremonyCodes.ValidationType.AdvancementEligibility);
        Assert.Equal(CeremonyCodes.ValidationStatus.Observed, advanceRequirement.GetProperty("status").GetString());

        var queueReadyResponse = await client.GetAsync(
            "/api/institutional/ceremonias/bandeja",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, queueReadyResponse.StatusCode);
        var queueReadyJson = await queueReadyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var queueReadyItem = queueReadyJson.GetProperty("items")
            .EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == ceremonyId);
        Assert.False(queueReadyItem.GetProperty("eligibility").GetProperty("canAuthorize").GetBoolean());
        Assert.False(queueReadyItem.GetProperty("actions").GetProperty("canAuthorize").GetBoolean());

        var authorizeResponse = await client.PostAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/autorizar",
            content: null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, authorizeResponse.StatusCode);

        var authorizeJson = await authorizeResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Contains("requisitos", authorizeJson.GetProperty("message").GetString() ?? string.Empty);
        var rejectionEligibility = authorizeJson.GetProperty("eligibility");
        Assert.False(rejectionEligibility.GetProperty("canAuthorize").GetBoolean());
        Assert.Contains(rejectionEligibility.GetProperty("requirements").EnumerateArray(),
            x => x.GetProperty("code").GetString() == CeremonyCodes.ValidationType.AdvancementEligibility &&
                 x.GetProperty("status").GetString() == CeremonyCodes.ValidationStatus.Observed);

        var queueAfterResponse = await client.GetAsync(
            "/api/institutional/ceremonias/bandeja",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, queueAfterResponse.StatusCode);
        var queueAfterJson = await queueAfterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var queueAfterItem = queueAfterJson.GetProperty("items")
            .EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == ceremonyId);
        Assert.Equal(CeremonyCodes.RequestStatus.Observed, queueAfterItem.GetProperty("status").GetString());
        Assert.True(queueAfterItem.GetProperty("actions").GetProperty("canValidateInternalAffairs").GetBoolean());
        Assert.False(queueAfterItem.GetProperty("actions").GetProperty("canAuthorize").GetBoolean());

        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var db = verificationScope.ServiceProvider.GetRequiredService<PmgmDbContext>();

            var persistedCeremony = await db.CeremonyRequests
                .AsNoTracking()
                .SingleAsync(x => x.Id == ceremonyId, cancellationToken);
            Assert.Equal(CeremonyCodes.RequestStatus.Observed, persistedCeremony.Status);
            Assert.Equal(2, await db.CeremonyRightPayments.CountAsync(x => x.CeremonyRequestId == ceremonyId, cancellationToken));

            var auditActions = await db.AuditEvents
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .Select(x => x.Action)
                .ToListAsync(cancellationToken);

            Assert.Contains("treasury.workshop_regularity.recorded", auditActions);
            Assert.Contains("hospitalaria.workshop_regularity.recorded", auditActions);
            Assert.Contains("ceremony.request.created", auditActions);
            Assert.Contains("treasury.ceremony_right.payment_recorded", auditActions);
            Assert.Contains("ceremony.internal_affairs_validation.recorded", auditActions);
            Assert.Contains("ceremony.grand_master_validation.recorded", auditActions);
            Assert.Contains("ceremony.authorization.rejected", auditActions);
            Assert.DoesNotContain("ceremony.authorization.approved", auditActions);
        }
    }

    [Fact]
    public async Task AdvancementAttendance_ReadsRealMeetingAndInstructionRecordsWithoutAuthorizing()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var token = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid ceremonyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var lodgeDb = scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
            await db.Database.MigrateAsync(token);

            var organization = new Organization
            {
                Name = $"Taller Evidencia CI {Guid.NewGuid():N}", Number = "EV-CI",
                Type = "workshop", City = "Santiago", Country = "Chile"
            };
            var person = new Person { FirstNames = "Evidencia", LastNames = "Aprendiz" };
            var member = new Member { Person = person, PersonId = person.Id };
            var membership = new Membership
            {
                Organization = organization, OrganizationId = organization.Id,
                Member = member, MemberId = member.Id, MembershipType = "regular",
                StartDate = new DateOnly(2026, 1, 1), Status = MembershipCodes.MembershipStatus.Active
            };
            var degree = new DegreeEvent
            {
                Organization = organization, OrganizationId = organization.Id,
                Member = member, MemberId = member.Id,
                Degree = "1", EventType = MembershipCodes.DegreeEvent.Initiation,
                EffectiveDate = new DateOnly(2026, 1, 15)
            };
            var ceremony = new CeremonyRequest
            {
                Organization = organization, OrganizationId = organization.Id,
                Member = member, MemberId = member.Id,
                CeremonyType = CeremonyCodes.Type.WageIncrease,
                Status = CeremonyCodes.RequestStatus.UnderReview
            };
            db.AddRange(organization, person, member, membership, degree, ceremony);
            await db.SaveChangesAsync(token);
            ceremonyId = ceremony.Id;

            var attended = new LodgeMeeting
            {
                OrganizationId = organization.Id, MeetingDate = new DateOnly(2026, 7, 3),
                Grade = LodgeManagementCodes.Grade.Apprentice, MeetingType = LodgeManagementCodes.MeetingType.Regular,
                Status = LodgeManagementCodes.MeetingStatus.Closed
            };
            var excused = new LodgeMeeting
            {
                OrganizationId = organization.Id, MeetingDate = new DateOnly(2026, 8, 3),
                Grade = LodgeManagementCodes.Grade.Apprentice, MeetingType = LodgeManagementCodes.MeetingType.Regular,
                Status = LodgeManagementCodes.MeetingStatus.Held
            };
            var cancelled = new LodgeMeeting
            {
                OrganizationId = organization.Id, MeetingDate = new DateOnly(2026, 8, 4),
                Grade = LodgeManagementCodes.Grade.Apprentice, MeetingType = LodgeManagementCodes.MeetingType.Regular,
                Status = LodgeManagementCodes.MeetingStatus.Cancelled
            };
            var instruction = new LodgeInstructionSession
            {
                OrganizationId = organization.Id, InstructionDate = new DateOnly(2026, 8, 6),
                Grade = LodgeManagementCodes.Grade.Apprentice, Topic = "Estudio del grado",
                ResponsibleOffice = LodgeManagementCodes.InstructionOffice.SecondWarden,
                Status = LodgeManagementCodes.InstructionStatus.Held,
                CreatedBySubject = "ci-evidence"
            };
            lodgeDb.AddRange(attended, excused, cancelled, instruction);
            await lodgeDb.SaveChangesAsync(token);

            lodgeDb.AddRange(
                new LodgeAttendanceRecord
                {
                    MeetingId = attended.Id, MemberId = member.Id,
                    Status = LodgeManagementCodes.AttendanceStatus.Absent
                },
                new LodgeAttendanceRecord
                {
                    MeetingId = attended.Id, MemberId = member.Id,
                    Status = LodgeManagementCodes.AttendanceStatus.Present
                },
                new LodgeAttendanceRecord
                {
                    MeetingId = excused.Id, MemberId = member.Id,
                    Status = LodgeManagementCodes.AttendanceStatus.Excused
                },
                new LodgeAttendanceRecord
                {
                    MeetingId = cancelled.Id, MemberId = member.Id,
                    Status = LodgeManagementCodes.AttendanceStatus.Present
                },
                new LodgeInstructionAttendanceRecord
                {
                    InstructionSessionId = instruction.Id, MemberId = member.Id,
                    Status = LodgeManagementCodes.InstructionAttendanceStatus.Present,
                    RecordedBySubject = "ci-evidence"
                });
            await lodgeDb.SaveChangesAsync(token);

            var documentsDb = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
            var collection = new DocumentCollection
            {
                Code = $"test-workpaper-{Guid.NewGuid():N}",
                Name = "Planchas CI",
                Scope = DocumentManagementCodes.Scope.Organization,
                OrganizationId = organization.Id,
                Status = DocumentManagementCodes.CollectionStatus.Active,
                CreatedBySubject = "ci-evidence"
            };
            var document = new InstitutionalDocument
            {
                Collection = collection,
                CollectionId = collection.Id,
                OrganizationId = organization.Id,
                AuthorMemberId = member.Id,
                DocumentType = "work_paper",
                Title = "Plancha con análisis limpio, aún sin presentación acreditada",
                Status = DocumentManagementCodes.DocumentStatus.Published,
                MinimumDegreeRequired = 1,
                CreatedBySubject = "ci-evidence"
            };
            var version = new DocumentVersion
            {
                Document = document,
                DocumentId = document.Id,
                VersionNumber = 1,
                OriginalFileName = "plancha.pdf",
                ContentType = "application/pdf",
                SizeBytes = 810,
                Sha256 = new string('b', 64),
                ScanReference = "SCAN-CI-OK",
                ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Available,
                AuthorEffectiveDegreeAtUpload = 1,
                CreatedAtUtc = new DateTimeOffset(2026, 8, 7, 13, 0, 0, TimeSpan.Zero),
                ObjectKey = $"documents/{document.Id:N}/{Guid.NewGuid():N}",
                CreatedBySubject = "ci-evidence"
            };
            // La versión debe existir antes de actualizar el puntero publicado:
            // core.institutional_documents.PublishedVersionId es una FK real.
            documentsDb.AddRange(collection, document, version);
            await documentsDb.SaveChangesAsync(token);
            document.PublishedVersionId = version.Id;
            await documentsDb.SaveChangesAsync(token);
        }

        var response = await client.GetAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/avance/asistencias", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
        Assert.Equal("ready", payload.GetProperty("status").GetString());
        Assert.False(payload.GetProperty("authorizesCeremony").GetBoolean());
        Assert.False(payload.GetProperty("workPapersEvaluated").GetBoolean());
        Assert.False(payload.GetProperty("includesExcusesInPresence").GetBoolean());
        var snapshot = payload.GetProperty("snapshot");
        Assert.Equal(new DateOnly(2026, 1, 15).ToString("yyyy-MM-dd"),
            snapshot.GetProperty("gradeStartDate").GetString());
        var meetings = snapshot.GetProperty("meetings");
        Assert.Equal(2, meetings.GetProperty("held").GetInt32());
        Assert.Equal(1, meetings.GetProperty("present").GetInt32());
        Assert.Equal(1, meetings.GetProperty("excused").GetInt32());
        var instructions = snapshot.GetProperty("instructions");
        Assert.Equal(1, instructions.GetProperty("present").GetInt32());

        var papersResponse = await client.GetAsync(
            $"/api/ceremonias/solicitudes/{ceremonyId}/avance/planchas", token);
        Assert.Equal(HttpStatusCode.OK, papersResponse.StatusCode);
        Assert.Contains("no-store", papersResponse.Headers.CacheControl?.ToString() ?? string.Empty);
        var papers = await papersResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
        Assert.False(papers.GetProperty("authorizesCeremony").GetBoolean());
        var summary = papers.GetProperty("workPapers");
        Assert.Equal(1, summary.GetProperty("totalDocuments").GetInt32());
        Assert.Equal(1, summary.GetProperty("reviewableDocuments").GetInt32());
        Assert.Equal(0, summary.GetProperty("confirmedPresented").GetInt32());
        Assert.False(summary.GetProperty("presentationEvidenceAvailable").GetBoolean());
        Assert.False(summary.GetProperty("items")[0].GetProperty("presentationVerified").GetBoolean());
        Assert.True(summary.GetProperty("items")[0].GetProperty("publishedToLibrary").GetBoolean());
    }

}

internal sealed class PmgmWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>();
            services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));

            services.RemoveAll<LodgeManagementDbContext>();
            services.RemoveAll<DbContextOptions<LodgeManagementDbContext>>();
            services.AddDbContext<LodgeManagementDbContext>(options => options.UseNpgsql(connectionString));
            services.RemoveAll<DocumentManagementDbContext>();
            services.RemoveAll<DbContextOptions<DocumentManagementDbContext>>();
            services.AddDbContext<DocumentManagementDbContext>(options => options.UseNpgsql(connectionString));

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

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PMGM-CI-Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("sub", "ci-http-admin"),
            new Claim(ClaimTypes.NameIdentifier, "ci-http-admin"),
            new Claim(ClaimTypes.Name, "CI HTTP Admin"),
            new Claim(InstitutionalClaims.Scope, "order"),
            new Claim(InstitutionalClaims.Role, InstitutionalRoles.GranLogiaAdmin)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
