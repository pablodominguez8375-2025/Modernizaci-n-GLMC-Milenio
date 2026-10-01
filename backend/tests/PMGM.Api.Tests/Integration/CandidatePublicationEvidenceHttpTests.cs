using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.CandidateIntake;
using PMGM.Api.Modules.CandidateIntake.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.DocumentManagement;
using PMGM.Api.Modules.DocumentManagement.Entities;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.Notifications;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class CandidatePublicationEvidenceHttpTests
{
    private static readonly byte[] PhotoA = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    private static readonly byte[] PhotoB = [.. PhotoA, 0];
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
        DateTimeOffset.UtcNow, "America/Santiago").DateTime);

    [Fact]
    public async Task Real_approval_pins_photo_and_policy_through_profile_edits_and_authorization()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var root = new CandidatePublicationTestFactory(connection);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInstitutionalNotificationService>();
            services.AddSingleton<IInstitutionalNotificationService, EvidenceNotificationSink>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = EvidenceAuthentication.SchemeName;
                options.DefaultChallengeScheme = EvidenceAuthentication.SchemeName;
                options.DefaultForbidScheme = EvidenceAuthentication.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, EvidenceAuthentication>(EvidenceAuthentication.SchemeName, _ => { });
        }));
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var intake = scope.ServiceProvider.GetRequiredService<CandidateIntakeDbContext>();
        var documents = scope.ServiceProvider.GetRequiredService<DocumentManagementDbContext>();
        await db.Database.MigrateAsync(ct); await intake.Database.MigrateAsync(ct); await documents.Database.MigrateAsync(ct);
        var code = CandidatePublicationEvidenceStore.FieldsCode;
        Assert.False(await db.InstitutionalRuleSettings.AnyAsync(x => x.Code == code, ct));
        try
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var workshop = new Organization { Name = $"Taller evidencia {suffix}", Number = suffix, Type = "workshop" };
            var person = new Person { FirstNames = $"Persona sintética {suffix}", LastNames = "Evidencia",
                Phone = "PRIVATE-PHONE", Email = "PRIVATE-EMAIL", Address = "PRIVATE-ADDRESS" };
            var ceremony = new CeremonyRequest { Organization = workshop, CandidatePerson = person,
                CeremonyType = CeremonyCodes.Type.Initiation, Status = CeremonyCodes.RequestStatus.UnderReview };
            db.AddRange(workshop, person, ceremony);
            db.CeremonyValidations.Add(new CeremonyValidation { CeremonyRequest = ceremony,
                ValidationType = CeremonyCodes.ValidationType.CandidateInitialDeliberation,
                Status = CeremonyCodes.ValidationStatus.Approved, AsOfDate = Today });
            var collection = new DocumentCollection { Code = $"EVIDENCE-{suffix}", Name = "Fotos sintéticas CI",
                Scope = DocumentManagementCodes.Scope.Organization, OrganizationId = workshop.Id };
            var document = new InstitutionalDocument { Collection = collection, OrganizationId = workshop.Id,
                Title = "Foto privada CI", DocumentType = "candidate_passport_photo",
                Classification = DocumentManagementCodes.Classification.Sensitive,
                AccessPolicy = DocumentManagementCodes.AccessPolicy.ManagementOnly,
                Status = DocumentManagementCodes.DocumentStatus.Active };
            var photos = new[] { PhotoA, PhotoB }.Select((bytes, index) => new DocumentVersion {
                Document = document, VersionNumber = index + 1, OriginalFileName = "PRIVATE-PHOTO.png",
                ContentType = "image/png", SizeBytes = bytes.Length, ObjectKey = $"PRIVATE/{Guid.NewGuid():N}",
                ProcessingStatus = DocumentManagementCodes.ProcessingStatus.Available,
                Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()
            }).ToArray();
            documents.AddRange(collection, document); documents.AddRange(photos);
            for (var i = 0; i < photos.Length; i++)
                await root.ObjectStore.StoreAsync(photos[i].ObjectKey, new MemoryStream(i == 0 ? PhotoA : PhotoB), "image/png", ct);
            var profile = new CandidateIntakeProfile { CeremonyRequestId = ceremony.Id,
                OrganizationId = workshop.Id, PersonId = person.Id, PaternalSurname = "Evidencia", RutOrInstitutionalId = "PRIVATE-ID",
                BirthDate = new DateOnly(1980, 1, 1), Nationality = "Sintética", CivilStatus = "Sintético", City = "Ciudad CI",
                Occupation = "Ocupación CI", EmployerName = "Empleador CI", WorkAddress = "PRIVATE-WORK",
                WorkPosition = "Cargo CI", WorkPhone = "PRIVATE-WORK-PHONE", Orient = "Oriente CI", PresentersJson = "[\"Presentante CI\"]",
                FirstDegreePresentationDate = Today, ResponsibleSecretaryName = "Secretaría CI", PhotoVersionId = photos[0].Id,
                SubmittedBySubject = "ci-evidence", UpdatedBySubject = "ci-evidence" };
            intake.Add(profile);
            await db.SaveChangesAsync(ct); await documents.SaveChangesAsync(ct); await intake.SaveChangesAsync(ct);
            var settingsUrl = $"/api/system/settings/{code}";
            Authenticate(client, workshop.Id, "member", "workshop");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(settingsUrl,
                new { value = CandidatePublicationEvidenceStore.DefaultFields, effectiveFrom = Today, sourceReference = "CI" }, ct)).StatusCode);
            Authenticate(client, workshop.Id, InstitutionalRoles.GranLogiaAdmin);
            foreach (var value in new[] { "Fotografía|Nombre completo|Taller|RUT", "Nombre completo|Taller" })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(settingsUrl,
                    new { value, effectiveFrom = Today, sourceReference = "CI inválida" }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(settingsUrl,
                new { value = CandidatePublicationEvidenceStore.DefaultFields, effectiveFrom = Today.AddDays(-1), sourceReference = "CI retroactiva" }, ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(settingsUrl,
                new { value = "Taller|nombre completo|fotografía", effectiveFrom = Today, sourceReference = "CI política inicial" }, ct)).StatusCode);
            var initial = await CandidatePublicationEvidenceStore.ResolvePolicyAsync(db, Today, ct);
            Assert.NotNull(initial.VersionId);
            Authenticate(client, workshop.Id, InstitutionalRoles.GranSecretaria);
            var publishUrl = $"/api/ceremonias/solicitudes/{ceremony.Id}/aprobar-publicacion-insinuado";
            var published = await client.PostAsync(publishUrl, null, ct);
            Assert.Equal(HttpStatusCode.Created, published.StatusCode);
            var publicationId = (await published.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("id").GetGuid();
            var approval = await db.AuditEvents.AsNoTracking().SingleAsync(x => x.EntityId == publicationId.ToString() &&
                x.Action == CandidatePublicationEvidenceStore.ApprovalAction, ct);
            var snapshot = Assert.IsType<CandidatePublicationFrozenEvidence>(CandidatePublicationEvidenceStore.ReadMetadata(approval.MetadataJson));
            Assert.Equal(photos[0].Id, snapshot.PhotoVersionId); Assert.Equal(initial.VersionId, snapshot.Policy.VersionId);
            profile.PhotoVersionId = photos[1].Id; await intake.SaveChangesAsync(ct);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(publishUrl, null, ct)).StatusCode);
            Authenticate(client, workshop.Id, InstitutionalRoles.GranLogiaAdmin);
            foreach (var days in new[] { 10, 20 })
                Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(settingsUrl,
                    new { value = CandidatePublicationEvidenceStore.DefaultFields, effectiveFrom = Today.AddDays(days), sourceReference = $"CI futura {days}" }, ct)).StatusCode);
            Assert.Equal(initial.VersionId, (await CandidatePublicationEvidenceStore.ResolvePolicyAsync(db, Today, ct)).VersionId);
            Assert.NotEqual(initial.VersionId, (await CandidatePublicationEvidenceStore.ResolvePolicyAsync(db, Today.AddDays(10), ct)).VersionId);
            var settings = await client.GetFromJsonAsync<JsonElement>("/api/system/settings/", ct);
            Assert.Equal("CI política inicial", settings.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("code").GetString() == code).GetProperty("sourceReference").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(settingsUrl,
                new { value = CandidatePublicationEvidenceStore.DefaultFields, effectiveFrom = Today.AddDays(15), sourceReference = "CI intermedia" }, ct)).StatusCode);
            Authenticate(client, Guid.NewGuid(), "member", "workshop");
            var photoUrl = $"/api/candidate-publications/{publicationId}/photo";
            Assert.Equal(PhotoA, await client.GetByteArrayAsync(photoUrl, ct));
            var portal = await client.GetFromJsonAsync<JsonElement>("/api/candidate-publications/active", ct);
            var item = portal.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("displayName").GetString() == $"{person.FirstNames} {person.LastNames}");
            Assert.Equal(photoUrl, item.GetProperty("photoUrl").GetString());
            Assert.DoesNotContain("PRIVATE-", item.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("photoVersionId", item.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Authenticate(client, workshop.Id, InstitutionalRoles.TallerSecretaria, "workshop");
            Assert.Equal(PhotoB, await client.GetByteArrayAsync($"/api/insinuados/solicitudes/{ceremony.Id}/foto", ct));
            profile.PhotoVersionId = null; await intake.SaveChangesAsync(ct);
            Assert.Equal(PhotoA, await client.GetByteArrayAsync(photoUrl, ct));
            var publication = await db.CandidatePublications.SingleAsync(x => x.Id == publicationId, ct);
            publication.PublishedFromUtc = DateTimeOffset.UtcNow.AddDays(-(publication.RequiredDays + 5));
            foreach (var type in new[] { CeremonyCodes.ValidationType.InternalAffairs, CeremonyCodes.ValidationType.GrandMaster })
                db.Add(new CeremonyValidation { CeremonyRequestId = ceremony.Id, ValidationType = type,
                    Status = CeremonyCodes.ValidationStatus.Approved, AsOfDate = Today });
            db.Add(new FinancialRegularitySnapshot { OrganizationId = workshop.Id, Scope = TreasuryCodes.RegularityScope.Organization,
                Status = TreasuryCodes.RegularityStatus.UpToDate, AsOfDate = Today });
            db.Add(new HospitalariaRegularitySnapshot { OrganizationId = workshop.Id,
                Status = HospitalariaCodes.RegularityStatus.UpToDate, AsOfDate = Today });
            var right = GrandTreasuryFeeSchedule.ResolveCeremonyRight(CeremonyCodes.Type.Initiation, Today)!.Value;
            db.Add(new CeremonyRightPayment { CeremonyRequestId = ceremony.Id, Amount = right.Amount, Currency = right.Currency,
                PaymentMethod = "transfer", PaymentDate = Today, ReceiptNumber = suffix, IdempotencyKey = suffix, RecordedBySubject = "ci-evidence" });
            await db.SaveChangesAsync(ct);
            Authenticate(client, workshop.Id, InstitutionalRoles.GranSecretaria);
            var authorizeUrl = $"/api/ceremonias/solicitudes/{ceremony.Id}/autorizar";
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(authorizeUrl, null, ct)).StatusCode);
            db.Add(new CeremonyValidation { CeremonyRequestId = ceremony.Id,
                ValidationType = CeremonyCodes.ValidationType.CandidateFinalBallot,
                Status = CeremonyCodes.ValidationStatus.Approved, AsOfDate = Today });
            await db.SaveChangesAsync(ct);
            var authorization = await client.PostAsync($"/api/ceremonias/solicitudes/{ceremony.Id}/autorizar", null, ct);
            Assert.True(authorization.StatusCode == HttpStatusCode.OK, await authorization.Content.ReadAsStringAsync(ct));
            var authorized = await db.AuditEvents.AsNoTracking().SingleAsync(x => x.Action == "ceremony.authorization.approved" && x.EntityId == ceremony.Id.ToString(), ct);
            var retained = Assert.IsType<CandidatePublicationFrozenEvidence>(CandidatePublicationEvidenceStore.ReadMetadata(authorized.MetadataJson));
            Assert.Equal(snapshot.PhotoVersionId, retained.PhotoVersionId); Assert.Equal(initial.VersionId, retained.Policy.VersionId);
            using var authorizationJson = JsonDocument.Parse(authorized.MetadataJson!);
            Assert.Equal("frozen", authorizationJson.RootElement.GetProperty("publicationEvidenceStatus").GetString());
            publication.PublishedUntilUtc = DateTimeOffset.UtcNow.AddDays(-1); await db.SaveChangesAsync(ct);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(photoUrl, ct)).StatusCode);
            // Historical fixtures have no approval snapshot. Do not invent past evidence.
            await db.Entry(ceremony).ReloadAsync(ct);
            publication.Status = CeremonyCodes.PublicationStatus.Cancelled;
            ceremony.Status = CeremonyCodes.RequestStatus.UnderReview;
            profile.PhotoVersionId = photos[1].Id;
            var legacy = new CandidatePublication { CeremonyRequestId = ceremony.Id, PersonId = person.Id,
                OrganizationId = workshop.Id, PublishedFromUtc = DateTimeOffset.UtcNow.AddDays(-(publication.RequiredDays + 4)), RequiredDays = publication.RequiredDays,
                RuleCode = CeremonyCodes.Rules.InitiationPublicationMinimumDays, Status = CeremonyCodes.PublicationStatus.Published };
            db.Add(legacy); await db.SaveChangesAsync(ct); await intake.SaveChangesAsync(ct);
            var legacyUrl = $"/api/candidate-publications/{legacy.Id}/photo";
            Assert.Equal(PhotoB, await client.GetByteArrayAsync(legacyUrl, ct));
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/ceremonias/solicitudes/{ceremony.Id}/autorizar", null, ct)).StatusCode);
            var legacyAuthorization = await db.AuditEvents.AsNoTracking().Where(x => x.Action == "ceremony.authorization.approved" &&
                x.EntityId == ceremony.Id.ToString() && x.Id != authorized.Id).SingleAsync(ct);

            using var legacyJson = JsonDocument.Parse(legacyAuthorization.MetadataJson!);
            Assert.Equal("legacy_not_recorded", legacyJson.RootElement.GetProperty("publicationEvidenceStatus").GetString());
            db.AuditEvents.Add(new AuditEvent { Action = CandidatePublicationEvidenceStore.ApprovalAction,
                EntityType = nameof(CandidatePublication), EntityId = legacy.Id.ToString(), Result = AuditResults.Success,
                CorrelationId = "ci-invalid-evidence", MetadataJson = "{\"publicationEvidence\":{\"schemaVersion\":99}}" });
            await db.SaveChangesAsync(ct);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(legacyUrl, ct)).StatusCode);
            var invalidPortal = await client.GetFromJsonAsync<JsonElement>("/api/candidate-publications/active", ct);
            Assert.Equal(JsonValueKind.Null, invalidPortal.GetProperty("items").EnumerateArray().Single(x =>
                x.GetProperty("displayName").GetString() == $"{person.FirstNames} {person.LastNames}").GetProperty("photoUrl").ValueKind);

        }
        finally
        {
            // The policy is global; serialized PostgreSQL fixtures leave the catalog clean.
            await db.InstitutionalRuleSettings.Where(x => x.Code == code).ExecuteDeleteAsync(ct);
        }
    }

    private static void Authenticate(HttpClient client, Guid organization, string role, string scope = "order")
    {
        client.DefaultRequestHeaders.Remove("X-Evidence-Organization"); client.DefaultRequestHeaders.Remove("X-Evidence-Role"); client.DefaultRequestHeaders.Remove("X-Evidence-Scope");
        client.DefaultRequestHeaders.Add("X-Evidence-Organization", organization.ToString());
        client.DefaultRequestHeaders.Add("X-Evidence-Role", role); client.DefaultRequestHeaders.Add("X-Evidence-Scope", scope);
    }
}

internal sealed class EvidenceNotificationSink : IInstitutionalNotificationService
{
    public Task<QueueNotificationResult> QueueAsync(QueueNotificationCommand command, CancellationToken cancellationToken)
        => Task.FromResult(new QueueNotificationResult(Guid.NewGuid(), false));
}

internal sealed class EvidenceAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PublicationEvidence-CI";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Evidence-Organization"].FirstOrDefault(), out var organization))
            return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "ci-evidence"),
            new Claim(ClaimTypes.NameIdentifier, "ci-evidence"), new Claim(InstitutionalClaims.Organization, organization.ToString()),
            new Claim(InstitutionalClaims.Role, Request.Headers["X-Evidence-Role"].FirstOrDefault() ?? "member"),
            new Claim(InstitutionalClaims.Scope, Request.Headers["X-Evidence-Scope"].FirstOrDefault() ?? "workshop") }, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
