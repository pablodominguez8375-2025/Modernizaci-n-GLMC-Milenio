using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class MemberSelfTreasuryCreditsPostgreSqlTests
{
    [Theory]
    [InlineData("CLP", "USD")]
    [InlineData("USD", "CLP")]
    public async Task Self_profile_projects_adjusted_own_credit_and_never_combines_currencies(string currency, string otherCurrency)
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connection);
        using var client = factory.CreateClient();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var receiptDate = new DateOnly(today.Year - 1, 12, 31);
        const string issuer = "urn:pmgm:unspecified-issuer", subject = "ci-http-admin";
        Guid correctedId, voidId, fullId, allocationId, otherMemberId, receiptOnlyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM core.member_identity_links WHERE \"Issuer\" = {issuer} AND \"Subject\" = {subject}", ct);
            var org = new Organization { Name = "Taller crédito ficticio", Number = $"CREDIT-{Guid.NewGuid():N}", Type = "workshop" };
            var historicalOrg = new Organization { Name = "Taller anterior ficticio", Number = $"OLD-{Guid.NewGuid():N}", Type = "workshop" };
            var member = new Member { Person = new Person { FirstNames = "Hermano", LastNames = "Crédito ficticio" }, InstitutionalNumber = $"CREDIT-{Guid.NewGuid():N}" };
            var other = new Member { Person = new Person { FirstNames = "Otro", LastNames = "Hermano ficticio" }, InstitutionalNumber = $"OTHER-{Guid.NewGuid():N}" };
            var membership = new Membership { Member = member, Organization = org, MembershipType = "regular", Status = "active", StartDate = today.AddYears(-1) };
            var degree = new DegreeEvent { Member = member, Organization = org, Degree = "3", EventType = "exaltation", EffectiveDate = today.AddYears(-1) };
            var plan = new LodgeFeePlan { Organization = org, FeeType = "normal", Currency = currency, MemberAmount = 100, GrandTreasuryAmount = 50, EffectiveFrom = new DateOnly(today.Year, 1, 1) };
            LodgeMemberCharge Charge(int month) => new() { Organization = org, Member = member, FeePlan = plan, Currency = currency, PeriodYear = today.Year, PeriodMonth = month, MemberAmount = 100, GrandTreasuryAmount = 50, Status = "pending" };
            LodgeMemberReceipt Receipt(Member owner, string code, string book, Guid workshop) => new() { OrganizationId = workshop, Member = owner, MemberId = owner.Id, Currency = book, Amount = 100, PaymentMethod = "transfer", PaymentDate = today, ReceiptNumber = $"{code}-{Guid.NewGuid():N}", IdempotencyKey = Guid.NewGuid().ToString(), RecordedBySubject = "ci-seed", Reference = "fictitious-bank-reference" };
            var corrected = Receipt(member, "OWN-CORRECTED", currency, org.Id);
            var annulled = Receipt(member, "OWN-VOID", currency, org.Id);
            var full = Receipt(member, "OWN-FULL", currency, org.Id);
            // Cash received in the previous year and allocated later must keep its receipt date.
            full.PaymentDate = receiptDate;
            var otherReceipt = Receipt(other, "OTHER-MEMBER-PRIVATE", currency, org.Id);
            var receiptOnly = Receipt(member, "OWN-RECEIPT-ONLY", otherCurrency, historicalOrg.Id);
            receiptOnly.Amount = 25;
            var correctionAllocation = new LodgeMemberPaymentAllocation { Receipt = corrected, Charge = Charge(1), Amount = 40, AllocatedBySubject = "ci-seed" };
            var voidAllocation = new LodgeMemberPaymentAllocation { Receipt = annulled, Charge = Charge(2), Amount = 40, AllocatedBySubject = "ci-seed" };
            var fullAllocation = new LodgeMemberPaymentAllocation { Receipt = full, Charge = Charge(3), Amount = 100, EffectiveDate = today, AllocatedBySubject = "ci-seed" };
            db.AddRange(org, historicalOrg, member, other, membership, degree, plan, corrected, annulled, full, otherReceipt, receiptOnly, correctionAllocation, voidAllocation, fullAllocation);
            await db.SaveChangesAsync(ct);
            var linkId = Guid.NewGuid(); var createdAt = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO core.member_identity_links ("Id", "MemberId", "Issuer", "Subject", "CreatedAtUtc", "CreatedBySubject", "RevokedAtUtc")
                VALUES ({linkId}, {member.Id}, {issuer}, {subject}, {createdAt}, {subject}, NULL)
                """, ct);
            correctedId = corrected.Id; voidId = annulled.Id; fullId = full.Id; allocationId = correctionAllocation.Id;
            otherMemberId = other.Id; receiptOnlyId = receiptOnly.Id;
        }

        var correction = new { kind = "correction", effectiveDate = today, reason = "Liberar a crédito ficticio", idempotencyKey = "self-credit-correction", allocationId, amount = 20m, allocations = Array.Empty<object>() };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/gestion-logial/tesoreria/recibos/{correctedId}/ajustes", correction, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/gestion-logial/tesoreria/recibos/{voidId}/ajustes", new { kind = "void", effectiveDate = today, reason = "Error ficticio", idempotencyKey = "self-credit-void", allocations = Array.Empty<object>() }, ct)).StatusCode);
        // A caller cannot switch whose self profile is read by supplying another member ID.
        var response = await client.GetAsync($"/api/member-self/profile?memberId={otherMemberId}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var account = json.GetProperty("treasuryAccount");
        var credits = account.GetProperty("unappliedCredits").EnumerateArray().ToList();
        Assert.Equal(2, credits.Count);
        var adjustedCredit = credits.Single(x => x.GetProperty("id").GetGuid() == correctedId);
        Assert.Equal(currency, adjustedCredit.GetProperty("currency").GetString());
        Assert.Equal(80m, adjustedCredit.GetProperty("amount").GetDecimal());
        var receiptOnlyCredit = credits.Single(x => x.GetProperty("id").GetGuid() == receiptOnlyId);
        Assert.Equal(otherCurrency, receiptOnlyCredit.GetProperty("currency").GetString());
        Assert.Equal(25m, receiptOnlyCredit.GetProperty("amount").GetDecimal());
        Assert.DoesNotContain(credits, x => x.GetProperty("id").GetGuid() == voidId || x.GetProperty("id").GetGuid() == fullId);
        Assert.DoesNotContain("OTHER-MEMBER-PRIVATE", json.GetRawText());
        Assert.All(credits, x => Assert.False(x.TryGetProperty("receiptDocumentId", out _)));
        // Available credit never silently pays another charge or offsets the debt balance.
        Assert.Equal(180m, account.GetProperty("balance").GetDecimal());
        Assert.Equal(120m, account.GetProperty("totalPaid").GetDecimal());
        var paidCharge = account.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("periodMonth").GetInt32() == 3);
        Assert.Equal(today.Year, paidCharge.GetProperty("periodYear").GetInt32());
        var projectedReceipt = Assert.Single(paidCharge.GetProperty("payments").EnumerateArray());
        Assert.Equal(fullId, projectedReceipt.GetProperty("id").GetGuid());
        Assert.Equal(receiptDate.ToString("yyyy-MM-dd"), projectedReceipt.GetProperty("paymentDate").GetString());
        Assert.Equal(currency, projectedReceipt.GetProperty("currency").GetString());
        Assert.Equal(100m, projectedReceipt.GetProperty("amount").GetDecimal());
        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(100m, (await verifyDb.LodgeMemberReceipts.SingleAsync(x => x.Id == correctedId, ct)).Amount);
        Assert.Equal(40m, (await verifyDb.LodgeMemberPaymentAllocations.SingleAsync(x => x.Id == allocationId, ct)).Amount);
        Assert.Equal(receiptDate, (await verifyDb.LodgeMemberReceipts.SingleAsync(x => x.Id == fullId, ct)).PaymentDate);
        Assert.Equal(today, (await verifyDb.LodgeMemberPaymentAllocations.SingleAsync(x => x.ReceiptId == fullId, ct)).EffectiveDate);
    }
}
