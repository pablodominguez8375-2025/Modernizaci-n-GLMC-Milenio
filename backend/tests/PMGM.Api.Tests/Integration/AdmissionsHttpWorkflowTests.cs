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
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.GrandSecretariat;
using PMGM.Api.Modules.GrandSecretariat.Entities;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.SecretariatOperations;
using PMGM.Api.Modules.SecretariatOperations.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AdmissionsHttpWorkflowTests
{
    [Fact]
    public async Task Transfer_affiliation_completion_preserves_source_history_and_is_idempotent()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new AdmissionsWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        var ceremonyDate = new DateOnly(2026, 9, 18);
        Guid sourceOrganizationId;
        Guid targetOrganizationId;
        Guid memberId;
        Guid sourceMembershipId;
        Guid admissionCaseId;
        Guid ceremonyId;
        Guid meetingId;
        Guid planchaId;
        Guid extractVersionId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var coreDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await coreDb.Database.MigrateAsync(cancellationToken);

            var source = new Organization
            {
                Name = $"Taller Origen Admisiones CI {Guid.NewGuid():N}",
                Number = "ADM-ORI",
                Type = "workshop"
            };
            var target = new Organization
            {
                Name = $"Taller Destino Admisiones CI {Guid.NewGuid():N}",
                Number = "ADM-DST",
                Type = "workshop"
            };
            var person = new Person
            {
                FirstNames = "Hermano",
                LastNames = "Traslado Integración"
            };
            var member = new Member
            {
                Person = person,
                PersonId = person.Id,
                InstitutionalNumber = $"ADM-{Guid.NewGuid():N}",
                CurrentDegree = "master"
            };
            var sourceMembership = new Membership
            {
                Member = member,
                MemberId = member.Id,
                Organization = source,
                OrganizationId = source.Id,
                MembershipType = "regular",
                StartDate = new DateOnly(2025, 3, 1),
                Status = MembershipCodes.MembershipStatus.Active,
                EvidenceReference = "CUADRO-ORIGEN-CI"
            };

            coreDb.AddRange(source, target, person, member, sourceMembership);
            await coreDb.SaveChangesAsync(cancellationToken);

            sourceOrganizationId = source.Id;
            targetOrganizationId = target.Id;
            memberId = member.Id;
            sourceMembershipId = sourceMembership.Id;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var admissionsDb = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>();
            var recordedBase = new DateTimeOffset(2026, 9, 9, 15, 0, 0, TimeSpan.Zero);
            var admissionCase = new AdmissionCase
            {
                OrganizationId = targetOrganizationId,
                AdmissionType = CeremonyCodes.Type.Affiliation,
                AffiliationMode = AdmissionCodes.AffiliationMode.Simple,
                AffiliationProcedure = AdmissionCodes.AffiliationProcedure.Transfer,
                MemberId = memberId,
                PersonId = await scope.ServiceProvider.GetRequiredService<PmgmDbContext>().Members
                    .Where(x => x.Id == memberId)
                    .Select(x => x.PersonId)
                    .SingleAsync(cancellationToken),
                OriginOrganizationId = sourceOrganizationId,
                OriginLodgeName = "Taller Origen Admisiones CI",
                Status = AdmissionWorkflowCodes.CaseStatus.UnderReview,
                CreatedBySubject = "ci-admission-secretaria",
                CreatedAtUtc = recordedBase.AddDays(-2)
            };

            admissionCase.Evidence.Add(new AdmissionEvidence
            {
                AdmissionCaseId = admissionCase.Id,
                EvidenceType = AdmissionWorkflowCodes.EvidenceType.WithdrawalLetter,
                SourceReference = "CRV-CI-001",
                ReviewStatus = CeremonyCodes.ValidationStatus.Approved,
                CreatedBySubject = "ci-admission-secretaria",
                CreatedAtUtc = recordedBase.AddDays(-1)
            });
            admissionCase.Decisions.Add(Decision(admissionCase.Id, AdmissionWorkflowCodes.DecisionType.WithdrawalLetterHandwrittenSignature, new DateOnly(2026, 9, 9), recordedBase, "CRV-FIRMA-CI"));
            admissionCase.Decisions.Add(Decision(admissionCase.Id, AdmissionWorkflowCodes.DecisionType.Article23Review, new DateOnly(2026, 9, 10), recordedBase.AddDays(1), "RI-2.3-CI"));
            admissionCase.Decisions.Add(Decision(admissionCase.Id, AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreePresentation, new DateOnly(2026, 9, 11), recordedBase.AddDays(2), "ACTA-1G-CI"));
            admissionCase.Decisions.Add(new AdmissionDecision
            {
                AdmissionCaseId = admissionCase.Id,
                DecisionType = AdmissionWorkflowCodes.DecisionType.InformationCommissionWaiver,
                Status = CeremonyCodes.ValidationStatus.Approved,
                AsOfDate = new DateOnly(2026, 9, 12),
                SourceReference = "ACTA-CAMARA-MEDIO-CI",
                StructuredDataJson = JsonSerializer.Serialize(new { waiver = true, basis = "article_2_5_transfer", authority = "camara_del_medio" }),
                RecordedBySubject = "ci-admission-secretaria",
                RecordedAtUtc = recordedBase.AddDays(3)
            });
            admissionCase.Decisions.Add(Decision(admissionCase.Id, AdmissionWorkflowCodes.DecisionType.LodgeThirdDegreeApproval, new DateOnly(2026, 9, 13), recordedBase.AddDays(4), "ACTA-3G-CI"));
            admissionCase.Decisions.Add(Decision(admissionCase.Id, AdmissionWorkflowCodes.DecisionType.LodgeFirstDegreeBallot, new DateOnly(2026, 9, 14), recordedBase.AddDays(5), "ACTA-BALOTAJE-CI"));

            admissionsDb.AdmissionCases.Add(admissionCase);
            await admissionsDb.SaveChangesAsync(cancellationToken);
            admissionCaseId = admissionCase.Id;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var coreDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var ceremony = new CeremonyRequest
            {
                OrganizationId = targetOrganizationId,
                CeremonyType = CeremonyCodes.Type.Affiliation,
                MemberId = memberId,
                AdmissionCaseId = admissionCaseId,
                ProposedDate = ceremonyDate,
                Status = CeremonyCodes.RequestStatus.Authorized
            };
            coreDb.CeremonyRequests.Add(ceremony);
            await coreDb.SaveChangesAsync(cancellationToken);
            ceremonyId = ceremony.Id;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var lodgeDb = scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
            var meeting = new LodgeMeeting
            {
                OrganizationId = targetOrganizationId,
                MeetingDate = ceremonyDate,
                MeetingType = LodgeManagementCodes.MeetingType.Solemn,
                Grade = LodgeManagementCodes.Grade.Master,
                CeremonyType = CeremonyCodes.Type.Affiliation,
                Modality = LodgeManagementCodes.MeetingModality.InPerson,
                LocationReference = "Templo CI",
                Title = "Tenida de Afiliación CI",
                Status = LodgeManagementCodes.MeetingStatus.Closed,
                HeldAtUtc = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero),
                ClosedAtUtc = new DateTimeOffset(2026, 9, 19, 1, 0, 0, TimeSpan.Zero)
            };
            lodgeDb.LodgeMeetings.Add(meeting);
            await lodgeDb.SaveChangesAsync(cancellationToken);
            meetingId = meeting.Id;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var grandSecretariatDb = scope.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>();
            var plancha = new SecretariatDocument
            {
                DocumentType = GrandSecretariatCodes.DocumentType.Plancha,
                PlanchaKind = GrandSecretariatCodes.PlanchaKind.CeremonyAuthorization,
                DocumentCode = $"PLA-AUT-CER-CI-{Guid.NewGuid():N}"[..28],
                Title = "Plancha de Autorización de Afiliación CI",
                Content = "Autorización institucional de prueba.",
                OrganizationId = targetOrganizationId,
                RelatedCeremonyRequestId = ceremonyId,
                Status = GrandSecretariatCodes.DocumentStatus.Issued,
                IssuedAtUtc = new DateTimeOffset(2026, 9, 17, 18, 0, 0, TimeSpan.Zero),
                IssuedBySubject = "ci-gran-secretaria"
            };
            grandSecretariatDb.SecretariatDocuments.Add(plancha);
            await grandSecretariatDb.SaveChangesAsync(cancellationToken);
            planchaId = plancha.Id;
        }

        extractVersionId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var coreDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            coreDb.LodgeSecretariatRecords.Add(new LodgeSecretariatRecord
            {
                OrganizationId = targetOrganizationId,
                RecordType = SecretariatOperationsCodes.RecordType.LodgeMeeting,
                SourceRecordId = meetingId,
                EventDate = ceremonyDate,
                Title = "Tenida de Afiliación CI",
                ExtractDocumentVersionId = extractVersionId,
                CeremonyAuthorizationDocumentId = planchaId,
                Status = SecretariatOperationsCodes.SubmissionStatus.Submitted,
                CreatedBySubject = "ci-admission-secretaria",
                SubmittedBySubject = "ci-admission-secretaria",
                SubmittedAtUtc = new DateTimeOffset(2026, 9, 19, 1, 5, 0, TimeSpan.Zero)
            });
            await coreDb.SaveChangesAsync(cancellationToken);
        }

        var first = await client.PostAsJsonAsync(
            $"/api/admisiones/ceremonias/{ceremonyId}/registrar-realizacion",
            new { meetingId, ceremonyDate },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.False(firstJson.GetProperty("alreadyCompleted").GetBoolean());
        Assert.Equal(memberId, firstJson.GetProperty("memberId").GetGuid());

        var retry = await client.PostAsJsonAsync(
            $"/api/admisiones/ceremonias/{ceremonyId}/registrar-realizacion",
            new { meetingId, ceremonyDate },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryJson = await retry.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.True(retryJson.GetProperty("alreadyCompleted").GetBoolean());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var coreDb = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var memberships = await coreDb.Memberships.AsNoTracking()
                .Where(x => x.MemberId == memberId)
                .OrderBy(x => x.StartDate)
                .ToListAsync(cancellationToken);

            Assert.Equal(2, memberships.Count);
            var source = Assert.Single(memberships, x => x.Id == sourceMembershipId);
            var target = Assert.Single(memberships, x => x.OrganizationId == targetOrganizationId);
            Assert.Equal(MembershipCodes.MembershipStatus.Transferred, source.Status);
            Assert.Equal(ceremonyDate.AddDays(-1), source.EndDate);
            Assert.Equal("Afiliación con traslado a otro Taller", source.EndReason);
            Assert.Equal(MembershipCodes.MembershipStatus.Active, target.Status);
            Assert.Equal(ceremonyDate, target.StartDate);
            Assert.Equal(memberId, target.MemberId);

            var transfer = await coreDb.MemberTransfers.AsNoTracking()
                .SingleAsync(x => x.MemberId == memberId && x.TargetOrganizationId == targetOrganizationId, cancellationToken);
            Assert.Equal(MembershipCodes.TransferStatus.Executed, transfer.Status);
            Assert.Equal(sourceMembershipId, transfer.SourceMembershipId);
            Assert.Equal(target.Id, transfer.TargetMembershipId);
            Assert.Equal(ceremonyDate, transfer.ApprovedEffectiveDate);

            var workshopTransferEvents = await coreDb.InstitutionalStatusEvents.AsNoTracking()
                .Where(x => x.MemberId == memberId && x.EventType == MembershipCodes.InstitutionalStatus.WorkshopTransfer)
                .ToListAsync(cancellationToken);
            Assert.Single(workshopTransferEvents);

            var ceremony = await coreDb.CeremonyRequests.AsNoTracking().SingleAsync(x => x.Id == ceremonyId, cancellationToken);
            Assert.Equal(CeremonyCodes.RequestStatus.Completed, ceremony.Status);
            Assert.Equal(memberId, ceremony.MemberId);

            var audit = await coreDb.AuditEvents.AsNoTracking()
                .Where(x => x.EntityId == ceremonyId.ToString() || x.Action == "membership.transfer.executed")
                .Select(x => new { x.Action, x.ActorSubject })
                .ToListAsync(cancellationToken);
            Assert.Equal(1, audit.Count(x => x.Action == "admission.ceremony.completed"));
            Assert.Equal(1, audit.Count(x => x.Action == "membership.transfer.executed"));
            Assert.Contains(audit, x => x.Action == "admission.ceremony.completed" && x.ActorSubject == "ci-http-admin");
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var admissionsDb = scope.ServiceProvider.GetRequiredService<AdmissionsDbContext>();
            var admissionCase = await admissionsDb.AdmissionCases.AsNoTracking()
                .Include(x => x.Decisions)
                .SingleAsync(x => x.Id == admissionCaseId, cancellationToken);

            Assert.Equal(AdmissionWorkflowCodes.CaseStatus.Resolved, admissionCase.Status);
            var completion = Assert.Single(admissionCase.Decisions, x => x.DecisionType == AdmissionWorkflowCodes.DecisionType.CeremonyCompleted);
            Assert.Equal("ci-http-admin", completion.RecordedBySubject);
            Assert.Equal(ceremonyId.ToString(), completion.SourceReference);
            Assert.Contains(meetingId.ToString(), completion.StructuredDataJson ?? string.Empty);
            Assert.Contains(planchaId.ToString(), completion.StructuredDataJson ?? string.Empty);
            Assert.Contains(extractVersionId.ToString(), completion.StructuredDataJson ?? string.Empty);
        }
    }

    private static AdmissionDecision Decision(
        Guid caseId,
        string type,
        DateOnly asOfDate,
        DateTimeOffset recordedAtUtc,
        string sourceReference)
        => new()
        {
            AdmissionCaseId = caseId,
            DecisionType = type,
            Status = CeremonyCodes.ValidationStatus.Approved,
            AsOfDate = asOfDate,
            SourceReference = sourceReference,
            RecordedBySubject = "ci-admission-secretaria",
            RecordedAtUtc = recordedAtUtc
        };
}

internal sealed class AdmissionsWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PmgmDbContext>();
            services.RemoveAll<DbContextOptions<PmgmDbContext>>();
            services.RemoveAll<AdmissionsDbContext>();
            services.RemoveAll<DbContextOptions<AdmissionsDbContext>>();
            services.RemoveAll<LodgeManagementDbContext>();
            services.RemoveAll<DbContextOptions<LodgeManagementDbContext>>();
            services.RemoveAll<GrandSecretariatDbContext>();
            services.RemoveAll<DbContextOptions<GrandSecretariatDbContext>>();

            services.AddDbContext<PmgmDbContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<AdmissionsDbContext>(options => options.UseNpgsql(connectionString));
            services.AddDbContext<LodgeManagementDbContext>(options => options.UseNpgsql(connectionString));
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
