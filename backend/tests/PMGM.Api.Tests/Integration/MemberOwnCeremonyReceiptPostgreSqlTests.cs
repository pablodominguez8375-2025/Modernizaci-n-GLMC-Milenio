using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Ceremonies;
using PMGM.Api.Modules.Ceremonies.Entities;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class MemberOwnCeremonyReceiptPostgreSqlTests
{
    private const string Issuer = "urn:pmgm:unspecified-issuer";
    private const string Subject = "ci-http-admin";

    [Fact]
    public async Task Member_only_lists_and_downloads_own_ceremony_receipts()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid ownReceiptId;
        Guid otherReceiptId;
        Guid unlinkedCandidateReceiptId;
        Guid ownMemberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM core.member_identity_links WHERE \"Issuer\" = {Issuer} AND \"Subject\" = {Subject}", ct);

            var ownOrg = new Organization { Name = "Taller Ceremonias QA", Type = "workshop" };
            var otherOrg = new Organization { Name = "Taller Ajeno Ceremonias QA", Type = "workshop" };
            var ownPerson = new Person { FirstNames = "Titular", LastNames = "Ficticio" };
            var otherPerson = new Person { FirstNames = "Ajeno", LastNames = "Ficticio" };
            var ownMember = new Member { Person = ownPerson, PersonId = ownPerson.Id };
            var otherMember = new Member { Person = otherPerson, PersonId = otherPerson.Id };
            // El resolver institucional exige enlace issuer/sub Y grado acreditado,
            // como los demás tests de Mi ficha. Sin DegreeEvent responde 404.
            var ownDegree = new DegreeEvent
            {
                Member = ownMember, MemberId = ownMember.Id,
                Organization = ownOrg, OrganizationId = ownOrg.Id,
                Degree = "3", EventType = MembershipCodes.DegreeEvent.Exaltation,
                EffectiveDate = new DateOnly(2022, 1, 10)
            };
            var ownCeremony = new CeremonyRequest
            {
                Organization = ownOrg, OrganizationId = ownOrg.Id,
                Member = ownMember, MemberId = ownMember.Id,
                CeremonyType = CeremonyCodes.Type.Exaltation,
                Status = CeremonyCodes.RequestStatus.Completed
            };
            var otherCeremony = new CeremonyRequest
            {
                Organization = otherOrg, OrganizationId = otherOrg.Id,
                Member = otherMember, MemberId = otherMember.Id,
                CeremonyType = CeremonyCodes.Type.WageIncrease,
                Status = CeremonyCodes.RequestStatus.Completed
            };
            // Matching PersonId alone is NOT proof of receipt ownership.
            var unlinkedCandidateCeremony = new CeremonyRequest
            {
                Organization = ownOrg, OrganizationId = ownOrg.Id,
                CandidatePerson = ownPerson, CandidatePersonId = ownPerson.Id,
                CeremonyType = CeremonyCodes.Type.Initiation,
                Status = CeremonyCodes.RequestStatus.Authorized
            };
            static CeremonyRightPayment Paid(CeremonyRequest ceremony, string number) => new()
            {
                CeremonyRequest = ceremony, CeremonyRequestId = ceremony.Id,
                Amount = 12500m, Currency = "CLP", PaymentMethod = "transfer",
                PaymentDate = new DateOnly(2026, 10, 18), ReceiptNumber = number,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                RecordedBySubject = "qa-treasury"
            };
            var own = Paid(ownCeremony, "CER-QA-PROPIO");
            var other = Paid(otherCeremony, "CER-QA-AJENO");
            var unlinked = Paid(unlinkedCandidateCeremony, "CER-QA-ANTERIOR");
            db.AddRange(ownOrg, otherOrg, ownPerson, otherPerson, ownMember, otherMember,
                ownDegree, ownCeremony, otherCeremony, unlinkedCandidateCeremony, own, other, unlinked);
            await db.SaveChangesAsync(ct);

            ownReceiptId = own.Id;
            otherReceiptId = other.Id;
            unlinkedCandidateReceiptId = unlinked.Id;
            ownMemberId = ownMember.Id;

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links
                    ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject", "RevokedAtUtc")
                VALUES
                    ({Guid.NewGuid()}, {ownMember.Id}, {Issuer}, {Subject}, {DateTimeOffset.UtcNow}, {Subject}, NULL)
                """, ct);
        }

        using var listing = await client.GetAsync("/api/membership/me/comprobantes/ceremonia", ct);
        Assert.Equal(HttpStatusCode.OK, listing.StatusCode);
        Assert.Contains("no-store", listing.Headers.CacheControl?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        var list = await listing.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        var row = list.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(ownReceiptId, row.GetProperty("id").GetGuid());
        Assert.Equal("CER-QA-PROPIO", row.GetProperty("receiptNumber").GetString());
        Assert.False(row.TryGetProperty("reference", out _));
        Assert.False(row.TryGetProperty("recordedBySubject", out _));

        using var ownResult = await client.GetAsync($"/api/membership/me/comprobantes/ceremonia/{ownReceiptId}", ct);
        Assert.Equal(HttpStatusCode.OK, ownResult.StatusCode);
        Assert.Contains("no-store", ownResult.Headers.CacheControl?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        var receipt = await ownResult.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        Assert.Equal("ceremonia", receipt.GetProperty("kind").GetString());
        Assert.Equal("CER-QA-PROPIO", receipt.GetProperty("receiptNumber").GetString());
        Assert.Equal("CLP", receipt.GetProperty("currency").GetString());
        Assert.Equal(12500m, receipt.GetProperty("receivedAmount").GetDecimal());
        Assert.Equal(0m, receipt.GetProperty("unallocatedAmount").GetDecimal());
        Assert.Contains("Exaltación", receipt.GetProperty("lines")[0].GetProperty("description").GetString());
        Assert.False(receipt.TryGetProperty("reference", out _));
        Assert.False(receipt.TryGetProperty("recordedBySubject", out _));

        using var foreignResult = await client.GetAsync($"/api/membership/me/comprobantes/ceremonia/{otherReceiptId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, foreignResult.StatusCode);
        using var candidateResult = await client.GetAsync($"/api/membership/me/comprobantes/ceremonia/{unlinkedCandidateReceiptId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, candidateResult.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var audit = await db.AuditEvents.AsNoTracking().Where(x =>
                x.Action == "member.self.receipt.downloaded" &&
                x.EntityType == "CeremonyRightPayment" &&
                x.EntityId == ownReceiptId.ToString()).ToListAsync(ct);
            Assert.Single(audit);
            Assert.True(await db.CeremonyRequests.AsNoTracking()
                .AnyAsync(x => x.MemberId == ownMemberId, ct));
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE core.member_identity_links SET \"RevokedAtUtc\" = {DateTimeOffset.UtcNow} WHERE \"Issuer\" = {Issuer} AND \"Subject\" = {Subject}", ct);
        }

        // The same authenticated session without an active member link is not sufficient.
        using var removedLinkList = await client.GetAsync("/api/membership/me/comprobantes/ceremonia", ct);
        Assert.Equal(HttpStatusCode.NotFound, removedLinkList.StatusCode);
        using var removedLinkReceipt = await client.GetAsync(
            $"/api/membership/me/comprobantes/ceremonia/{ownReceiptId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, removedLinkReceipt.StatusCode);
    }
}
