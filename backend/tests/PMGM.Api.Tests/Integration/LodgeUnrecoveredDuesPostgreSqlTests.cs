using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LodgeUnrecoveredDuesPostgreSqlTests
{
    [Theory]
    [InlineData("CLP")]
    [InlineData("USD")]
    public async Task Formal_nonpayment_loss_preserves_cash_dues_partial_payments_and_history(string currency)
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connection);
        using var client = factory.CreateClient();
        var today = GrandTreasuryTariff.Today();
        Guid organizationId, withdrawalId, voluntaryId, pendingId, partialId, futureId, memberId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>(); await db.Database.MigrateAsync(ct);
            var org = new Organization { Name = "Pérdidas ficticias", Number = $"LOSS-{Guid.NewGuid():N}", Type = "workshop" };
            var member = new Member { Person = new Person { FirstNames = "Hermano", LastNames = "Ficticio" } };
            var plan = new LodgeFeePlan { Organization = org, FeeType = "normal", MemberAmount = 100, GrandTreasuryAmount = 40, Currency = currency, EffectiveFrom = today.AddYears(-2) };
            var partial = new LodgeMemberCharge { Organization = org, Member = member, FeePlan = plan, PeriodYear = today.Year - 1, PeriodMonth = 1, MemberAmount = 100, GrandTreasuryAmount = 40, Currency = currency, Status = "partial" };
            var unpaid = new LodgeMemberCharge { Organization = org, Member = member, FeePlan = plan, PeriodYear = today.Year - 1, PeriodMonth = 2, MemberAmount = 100, GrandTreasuryAmount = 40, Currency = currency, Status = "pending" };
            var future = new LodgeMemberCharge { Organization = org, Member = member, FeePlan = plan, PeriodYear = today.Year + 1, PeriodMonth = 1, MemberAmount = 100, GrandTreasuryAmount = 40, Currency = currency, Status = "pending" };
            var paid = new LodgeMemberCharge { Organization = org, Member = member, FeePlan = plan, PeriodYear = today.Year - 1, PeriodMonth = 3, MemberAmount = 100, GrandTreasuryAmount = 40, Currency = currency, Status = "paid" };
            var receipt = new LodgeMemberReceipt { OrganizationId = org.Id, Member = member, Amount = 40, Currency = currency, PaymentDate = today, PaymentMethod = "transfer", ReceiptNumber = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), RecordedBySubject = "ci-seed" };
            receipt.Allocations.Add(new LodgeMemberPaymentAllocation { Receipt = receipt, Charge = partial, Amount = 40, AllocatedBySubject = "ci-seed" });
            paid.Payments.Add(new LodgeMemberPayment { Charge = paid, Amount = 100, Currency = currency, PaymentDate = today, PaymentMethod = "cash", ReceiptNumber = Guid.NewGuid().ToString(), RecordedBySubject = "ci-seed" });
            MemberWithdrawalRequest Withdrawal(string type,string status) => new() { Member = member, OriginOrganization = org, WithdrawalType = type, Status = status, RequestedEffectiveDate = today.AddDays(-1), Reason = "Causa verificada en resolución", EvidenceReference = "RES-FICTICIA", RequestedBySubject = "ci-seed" };
            var withdrawal = Withdrawal("forced","approved"); var voluntary = Withdrawal("voluntary","approved"); var pending = Withdrawal("forced","pending");
            db.AddRange(org, member, plan, partial, unpaid, future, paid, receipt, withdrawal, voluntary, pending);
            await db.SaveChangesAsync(ct);
            organizationId = org.Id; memberId = member.Id; withdrawalId = withdrawal.Id; voluntaryId = voluntary.Id; pendingId = pending.Id; partialId = partial.Id; futureId = future.Id;
        }
        var path = $"/api/gestion-logial/tesoreria/talleres/{organizationId}/perdidas";
        object Request(Guid id,bool confirmed=true) => new { withdrawalRequestId = id, recognitionDate = today, currency, nonPaymentConfirmed = confirmed, evidenceReference = "RES-NO-PAGO-FICTICIA" };
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(path,Request(withdrawalId,false),ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path,Request(voluntaryId),ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path,Request(pendingId),ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/gestion-logial/tesoreria/talleres/{Guid.NewGuid()}/perdidas",Request(withdrawalId),ct)).StatusCode);
        var reportPath=$"/api/gestion-logial/tesoreria/talleres/{organizationId}/reportes?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&currencyCode={currency}";
        var before = await client.GetFromJsonAsync<JsonElement>(reportPath,ct);
        Assert.Equal(140,before.GetProperty("income").GetDecimal());
        var created=await client.PostAsJsonAsync(path,Request(withdrawalId),ct); Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var result=await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
        Assert.Equal(160,result.GetProperty("total").GetDecimal()); Assert.Equal(2,result.GetProperty("items").GetArrayLength());
        var partialRow=result.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("chargeId").GetGuid()==partialId);
        Assert.Equal(40,partialRow.GetProperty("paidAmount").GetDecimal()); Assert.Equal(60,partialRow.GetProperty("amount").GetDecimal());
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(path,Request(withdrawalId),ct)).StatusCode);
        var after=await client.GetFromJsonAsync<JsonElement>(reportPath,ct);
        Assert.Equal(160,after.GetProperty("unrecoveredDuesTotal").GetDecimal());
        foreach(var field in new[]{"income","closingBalance","authorizedExpenses","pendingExpenses"}) Assert.Equal(before.GetProperty(field).GetDecimal(),after.GetProperty(field).GetDecimal());
        Assert.Equal(before.GetProperty("movements").GetArrayLength(),after.GetProperty("movements").GetArrayLength());
        var otherCurrency=currency=="CLP"?"USD":"CLP";
        var filtered=await client.GetFromJsonAsync<JsonElement>(reportPath.Replace($"currencyCode={currency}",$"currencyCode={otherCurrency}"),ct);
        Assert.Equal(0,filtered.GetProperty("unrecoveredDuesTotal").GetDecimal());
        await using var verify=factory.Services.CreateAsyncScope(); var dbVerify=verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(2,await dbVerify.LodgeUnrecoveredDues.CountAsync(x=>x.OrganizationId==organizationId,ct));
        Assert.False(await dbVerify.LodgeUnrecoveredDues.AnyAsync(x=>x.ChargeId==futureId,ct));
        var charge=await dbVerify.LodgeMemberCharges.Include(x=>x.Allocations).SingleAsync(x=>x.Id==partialId,ct);
        Assert.Equal(100,charge.MemberAmount); Assert.Equal(40,charge.Allocations.Sum(x=>x.Amount)); Assert.Equal("partial",charge.Status);
        Assert.Equal(1,await dbVerify.AuditEvents.CountAsync(x=>x.OrganizationId==organizationId && x.Action=="lodge.treasury.unrecovered_dues.recorded",ct));
        var lossId=partialRow.GetProperty("id").GetGuid();
        await Assert.ThrowsAsync<PostgresException>(()=>dbVerify.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM core.lodge_unrecovered_dues WHERE \"Id\" = {lossId}",ct));
    }
}
