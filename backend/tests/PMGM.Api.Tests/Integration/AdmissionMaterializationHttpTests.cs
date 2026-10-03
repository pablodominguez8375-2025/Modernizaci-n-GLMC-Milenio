using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMGM.Api.Data;
using PMGM.Api.Modules.Admissions;
using PMGM.Api.Modules.Admissions.Entities;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.GrandSecretariat.Entities;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.SecretariatOperations.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;
[Collection(PostgresIntegrationCollection.Name)]
public sealed class AdmissionMaterializationHttpTests
{
    [Theory]
    [InlineData("affiliation")]
    [InlineData("incorporation")]
    public async Task Closed_meeting_atomic_rollback_and_single_receipt(string type)
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES"); if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken; var failure = new FailFinalWrite();
        using var root = new PmgmWebApplicationFactory(connection);
        using var factory = root.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AdmissionsDbContext>(); services.RemoveAll<DbContextOptions<AdmissionsDbContext>>();
            services.AddDbContext<AdmissionsDbContext>(o => o.UseNpgsql(connection).AddInterceptors(failure));
            services.RemoveAll<GrandSecretariatDbContext>(); services.RemoveAll<DbContextOptions<GrandSecretariatDbContext>>();
            services.AddDbContext<GrandSecretariatDbContext>(o => o.UseNpgsql(connection));
        }));
        using var client = factory.CreateClient(); var today = AdmissionWithdrawalEvidencePolicy.ChileDate(DateTimeOffset.UtcNow); var created = DateTimeOffset.UtcNow.AddDays(-5);
        var org = new Organization { Name = "Materialization synthetic", Type = "workshop" };
        var person = new Person { FirstNames = "Synthetic", LastNames = "Materialization" }; var member = new Member { PersonId = person.Id, CurrentDegree = "master" };
        var origin = new Organization { Name = "CRV origin synthetic", Type = "workshop" };
        var segment = new PMGM.Api.Modules.Membership.Entities.Membership { MemberId = member.Id, OrganizationId = origin.Id,
            MembershipType = "regular", StartDate = today.AddYears(-1), EndDate = today.AddMonths(-1), Status = "closed", EndReason = "Retiro voluntario — sueño" };
        var withdrawal = new MemberWithdrawalRequest { MemberId = member.Id, OriginOrganizationId = origin.Id, WithdrawalType = "voluntary",
            RequestedEffectiveDate = today.AddMonths(-1), Reason = "Synthetic CRV", EvidenceReference = "Synthetic CRV", Status = "approved",
            OratorSignatureSubject = "synthetic-orator", OratorSignedAtUtc = created };
        var c = new AdmissionCase { OrganizationId = org.Id, PersonId = person.Id, MemberId = type == "affiliation" ? member.Id : null,
            AdmissionType = type, AffiliationMode = type == "affiliation" ? "simple" : null, WithdrawalLetterGrantedDate = today.AddMonths(-1),
            Degree = "master", HasPeaceAndFriendshipPact = true, Status = "eligible", CreatedBySubject = "synthetic", CreatedAtUtc = created };
        var letter = new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = "withdrawal_letter", DocumentVersionId = Guid.NewGuid(), EvidenceDate = c.WithdrawalLetterGrantedDate,
            ReviewStatus = "approved", ReviewedAtUtc = created.AddMinutes(1), CreatedBySubject = "synthetic" }; c.Evidence.Add(letter);
        if (type == "incorporation") foreach (var code in new[] { "legalized_initiation_evidence", "degree_evidence" })
            c.Evidence.Add(new AdmissionEvidence { AdmissionCaseId = c.Id, EvidenceType = code, DocumentVersionId = Guid.NewGuid(), ReviewStatus = "approved", ReviewedAtUtc = created.AddMinutes(1), CreatedBySubject = "synthetic" });
        void Add(string code, int days, int hour) => c.Decisions.Add(new AdmissionDecision { AdmissionCaseId = c.Id, DecisionType = code, Status = "approved", AsOfDate = today.AddDays(days),
            SourceReference = "SYNTHETIC ACTA", RecordedBySubject = "synthetic", RecordedAtUtc = created.AddHours(hour) });
        Add(AdmissionWorkflowCodes.DecisionType.WithdrawalSignature(letter.Id), -4, 1); Add("article_2_3_review", -4, 1);
        Add("information_commission_appointed", -4, 1); Add("information_commission_completed", -3, 2); Add("lodge_first_degree_presentation", -4, 1); Add("lodge_third_degree_approval", -2, 3); Add("lodge_first_degree_ballot", -1, 4);
        Assert.True(AdmissionCaseEligibilityProjector.Evaluate(c).Decision.CanProceed);
        var ceremony = new CeremonyRequest { OrganizationId = org.Id, CeremonyType = type, AdmissionCaseId = c.Id, MemberId = c.MemberId, ProposedDate = today, Status = "authorized" };
        var plancha = new SecretariatDocument { OrganizationId = org.Id, RelatedCeremonyRequestId = ceremony.Id, DocumentType = "plancha", PlanchaKind = "ceremony_authorization",
            DocumentCode = "SYN-" + Guid.NewGuid(), Title = "Synthetic", Content = "Synthetic", Status = "issued", IssuedBySubject = "synthetic", IssuedAtUtc = DateTimeOffset.UtcNow };
        var meeting = new LodgeMeeting { OrganizationId = org.Id, MeetingDate = today, MeetingType = "ordinary", Grade = "apprentice", CeremonyType = type, Status = "held" };
        var record = new LodgeSecretariatRecord { OrganizationId = org.Id, RecordType = "tenida", SourceRecordId = meeting.Id, EventDate = today, Title = "Synthetic", Status = "submitted", CreatedBySubject = "synthetic",
            FullMinuteDocumentVersionId = Guid.NewGuid(), ExtractDocumentVersionId = Guid.NewGuid(), CeremonyAuthorizationDocumentId = plancha.Id };
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var core = seed.ServiceProvider.GetRequiredService<PmgmDbContext>(); await core.Database.MigrateAsync(ct); core.AddRange(org, person); if (type == "affiliation") core.AddRange(member, origin, segment, withdrawal); await core.SaveChangesAsync(ct);
            var db = seed.ServiceProvider.GetRequiredService<AdmissionsDbContext>(); await db.Database.MigrateAsync(ct); db.Add(c); await db.SaveChangesAsync(ct); core.AddRange(ceremony, record); await core.SaveChangesAsync(ct);
            var gs = seed.ServiceProvider.GetRequiredService<GrandSecretariatDbContext>(); await gs.Database.MigrateAsync(ct); gs.Add(plancha); await gs.SaveChangesAsync(ct);
            var lodge = seed.ServiceProvider.GetRequiredService<LodgeManagementDbContext>(); await lodge.Database.MigrateAsync(ct); lodge.Add(meeting); await lodge.SaveChangesAsync(ct);
        }
        var payload = new MaterializeAdmissionRequest(today, "SYNTHETIC RESOLUTION");
        async Task<HttpResponseMessage> Post(MaterializeAdmissionRequest p) => await client.PostAsJsonAsync($"/api/admisiones/expedientes/{c.Id}/materializar", p, ct);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(payload)).StatusCode);
        await using (var close = factory.Services.CreateAsyncScope()) { var lodge = close.ServiceProvider.GetRequiredService<LodgeManagementDbContext>(); (await lodge.LodgeMeetings.SingleAsync(x => x.Id == meeting.Id, ct)).Status = "closed"; await lodge.SaveChangesAsync(ct); }
        failure.Enabled = true; try { Assert.Equal(HttpStatusCode.Conflict, (await Post(payload)).StatusCode); } finally { failure.Enabled = false; }
        await using (var check = factory.Services.CreateAsyncScope())
        {
            var core = check.ServiceProvider.GetRequiredService<PmgmDbContext>(); Assert.Equal(0, await core.Memberships.CountAsync(x => x.OrganizationId == org.Id, ct));
            Assert.Equal(type == "affiliation" ? 1 : 0, await core.Members.CountAsync(x => x.PersonId == person.Id, ct));
            Assert.Equal(0, await core.InstitutionalStatusEvents.CountAsync(x => x.OrganizationId == org.Id, ct));
            Assert.Equal(0, await core.AuditEvents.CountAsync(x => x.OrganizationId == org.Id && x.Action == "admission.membership.materialized", ct));
            Assert.Equal("eligible", (await check.ServiceProvider.GetRequiredService<AdmissionsDbContext>().AdmissionCases.SingleAsync(x => x.Id == c.Id, ct)).Status);
            Assert.Equal("authorized", (await core.CeremonyRequests.SingleAsync(x => x.Id == ceremony.Id, ct)).Status);
        }
        Guid? transferId = null;
        if (type == "affiliation")
        {
            var transferInput = new RequestTransferRequest(segment.Id, org.Id, today, "SYNTHETIC", "UNTRUSTED PAYLOAD", withdrawal.Id);
            var requested = await client.PostAsJsonAsync($"/api/members/{member.Id}/transfers/", transferInput, ct);
            Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
            transferId = (await requested.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/members/{member.Id}/transfers/{transferId}/approve", new ApproveTransferRequest(today, "SYNTHETIC"), ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/members/{member.Id}/transfers/{transferId}/execute", null, ct)).StatusCode);
        }
        var first = await Post(payload); Assert.Equal(HttpStatusCode.OK, first.StatusCode); Assert.True(first.Headers.CacheControl?.NoStore);
        var receipt = await first.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var replay = await Post(payload); Assert.Equal(HttpStatusCode.OK, replay.StatusCode); var replayReceipt = await replay.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.True(replayReceipt.GetProperty("idempotent").GetBoolean()); Assert.Equal(receipt.GetProperty("membershipId").GetGuid(), replayReceipt.GetProperty("membershipId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(payload with { EvidenceReference = "OTHER" })).StatusCode); Assert.Equal(HttpStatusCode.Conflict, (await Post(payload with { EffectiveDate = today.AddDays(-1) })).StatusCode);
        await using var final = factory.Services.CreateAsyncScope(); var savedCore = final.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(1, await savedCore.Memberships.CountAsync(x => x.OrganizationId == org.Id, ct)); Assert.Equal(1, await savedCore.Members.CountAsync(x => x.PersonId == person.Id, ct));
        Assert.Equal(1, await savedCore.InstitutionalStatusEvents.CountAsync(x => x.OrganizationId == org.Id, ct)); Assert.Equal(1, await savedCore.AuditEvents.CountAsync(x => x.OrganizationId == org.Id && x.Action == "admission.membership.materialized", ct));
        var saved = await final.ServiceProvider.GetRequiredService<AdmissionsDbContext>().AdmissionCases.Include(x => x.Decisions).SingleAsync(x => x.Id == c.Id, ct);
        Assert.Equal("resolved", saved.Status); Assert.Single(saved.Decisions, x => x.DecisionType == "membership_materialized"); Assert.Equal(receipt.GetProperty("memberId").GetGuid(), saved.MemberId);
        if (transferId is not null)
        {
            var path = $"/api/members/{member.Id}/transfers/{transferId}/execute";
            var executed = await client.PostAsync(path, null, ct); Assert.Equal(HttpStatusCode.OK, executed.StatusCode);
            var execution = await executed.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            Assert.Equal(receipt.GetProperty("membershipId").GetGuid(), execution.GetProperty("targetMembershipId").GetGuid());
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(path, null, ct)).StatusCode);
            await using var transferCheck = factory.Services.CreateAsyncScope(); var transferDb = transferCheck.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.Equal(1, await transferDb.Memberships.CountAsync(x => x.OrganizationId == org.Id, ct));
            var preserved = await transferDb.Memberships.SingleAsync(x => x.Id == segment.Id, ct);
            Assert.Equal(segment.EndDate, preserved.EndDate); Assert.Equal(segment.EndReason, preserved.EndReason); Assert.Equal("closed", preserved.Status);
            Assert.Equal(1, await transferDb.InstitutionalStatusEvents.CountAsync(x => x.OrganizationId == org.Id && x.EventType == MembershipCodes.InstitutionalStatus.WorkshopTransfer, ct));
            Assert.Equal(1, await transferDb.AuditEvents.CountAsync(x => x.Action == "membership.transfer.executed" && x.OrganizationId == origin.Id, ct));
            var transfer = await transferDb.MemberTransfers.SingleAsync(x => x.Id == transferId, ct);
            Assert.Equal(withdrawal.EvidenceReference, transfer.EvidenceReference);
        }

    }
    private sealed class FailFinalWrite : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<AdmissionDecision>().Any(x => x.Entity.DecisionType == "membership_materialized"))
                throw new DbUpdateException("Synthetic rollback after core save", new Npgsql.PostgresException("Synthetic conflict", "ERROR", "ERROR", "40001"));
            return ValueTask.FromResult(result);
        }
    }
}
