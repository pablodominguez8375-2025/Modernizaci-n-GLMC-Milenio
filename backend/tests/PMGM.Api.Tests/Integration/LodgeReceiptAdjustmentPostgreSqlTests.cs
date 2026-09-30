using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LodgeReceiptAdjustmentPostgreSqlTests
{
    [Fact]
    public async Task Correction_and_void_are_append_only_and_preserve_closed_year_cash_and_snapshots()
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connection);
        using var client = factory.CreateClient();
        Guid receiptId, sourceId, targetId, allocationId, organizationId, otherId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var org = new Organization { Name = "Receipt adjustments CI", Number = $"ADJ-{Guid.NewGuid():N}", Type = "workshop" };
            var person = new Person { FirstNames = "Hermano", LastNames = "Ficticio" };
            var member = new Member { Person = person, InstitutionalNumber = $"ADJ-{Guid.NewGuid():N}" };
            var other = new Member { Person = new Person { FirstNames = "Otro", LastNames = "Ficticio" }, InstitutionalNumber = $"ADJ-{Guid.NewGuid():N}" };
            var plan = new LodgeFeePlan { Organization = org, FeeType = "normal", MemberAmount = 10000, GrandTreasuryAmount = 5000, EffectiveFrom = new DateOnly(2025,1,1) };
            LodgeMemberCharge Charge(Member m, int year, int month) => new() { Organization = org, Member = m, FeePlan = plan, PeriodYear = year, PeriodMonth = month, MemberAmount = 10000, GrandTreasuryAmount = 5000, Status = "pending" };
            var source = Charge(member, 2025, 12); var target = Charge(member, 2027, 1); var forbidden = Charge(other, 2027, 1);
            var receipt = new LodgeMemberReceipt { OrganizationId = org.Id, Member = member, MemberId = member.Id, Amount = 20000, PaymentMethod = "transfer", PaymentDate = new DateOnly(2025,12,20), ReceiptNumber = $"CI-{Guid.NewGuid():N}", IdempotencyKey = Guid.NewGuid().ToString(), RecordedBySubject = "ci-seed" };
            var allocation = new LodgeMemberPaymentAllocation { Receipt = receipt, Charge = source, Amount = 7000, AllocatedBySubject = "ci-seed" };
            var closure = new LodgeTreasuryYearClosure { OrganizationId = org.Id, AccountingYear = 2025, OpeningBalance = 0, Income = 20000, AuthorizedExpenses = 0, ClosingBalance = 20000, MovementCount = 1, ClosedBySubject = "ci-seed" };
            db.AddRange(org, member, other, plan, source, target, forbidden, receipt, allocation, closure);
            await db.SaveChangesAsync(ct);
            receiptId=receipt.Id; sourceId=source.Id; targetId=target.Id; otherId=forbidden.Id; allocationId=allocation.Id; organizationId=org.Id;
        }
        var reconciliationResponse=await client.PostAsJsonAsync($"/api/gestion-logial/tesoreria/talleres/{organizationId}/conciliaciones",new {from=new DateOnly(2025,1,1),to=new DateOnly(2025,12,31),observedBalance=20000m},ct);
        Assert.Equal(HttpStatusCode.Created,reconciliationResponse.StatusCode);
        var path=$"/api/gestion-logial/tesoreria/recibos/{receiptId}/ajustes";
        object Correction(string key, decimal amount, Guid target, int year=2026) => new { kind="correction", effectiveDate=new DateOnly(year,9,29), reason="Período equivocado", idempotencyKey=key, allocationId, amount, allocations=new[]{new {chargeId=target,amount}} };
        var closed=await client.PostAsJsonAsync(path,Correction("closed",3000,targetId,2025),ct);
        Assert.Equal(HttpStatusCode.BadRequest,closed.StatusCode); // before original reception
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path,new {kind="void",effectiveDate=new DateOnly(2025,12,25),reason="Error",idempotencyKey="closed-year",allocations=Array.Empty<object>()},ct)).StatusCode);
        var forbiddenResponse=await client.PostAsJsonAsync(path,Correction("other",3000,otherId),ct);
        Assert.Equal(HttpStatusCode.BadRequest,forbiddenResponse.StatusCode);
        var correction=Correction("correct",3000,targetId);
        var response=await client.PostAsJsonAsync(path,correction,ct);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var saved=await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
        Assert.Equal(0,saved.GetProperty("cashAmount").GetDecimal());
        Assert.Equal("ci-http-admin",saved.GetProperty("recordedBySubject").GetString());
        var replay=await client.PostAsJsonAsync(path,correction,ct);
        Assert.Equal(HttpStatusCode.OK,replay.StatusCode);
        Assert.Equal(saved.GetProperty("id").GetGuid(),(await replay.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct)).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path,Correction("correct",2000,targetId),ct)).StatusCode);
        var oldCharges=await client.GetFromJsonAsync<JsonElement>($"/api/gestion-logial/tesoreria/talleres/{organizationId}/cargos?year=2025&month=12",ct);
        var oldSource=oldCharges.GetProperty("items").EnumerateArray().SelectMany(x=>x.GetProperty("periods").EnumerateArray()).Single(x=>x.GetProperty("chargeId").GetGuid()==sourceId);
        Assert.Equal(7000,oldSource.GetProperty("paidAmount").GetDecimal());
        var voidRequest=new {kind="void",effectiveDate=new DateOnly(2026,9,30),reason="Duplicado sin segundo cobro",idempotencyKey="void",allocations=Array.Empty<object>()};
        var voidResponse=await client.PostAsJsonAsync(path,voidRequest,ct);
        Assert.Equal(HttpStatusCode.Created,voidResponse.StatusCode);
        var voidSaved=await voidResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
        Assert.Equal(-20000,voidSaved.GetProperty("cashAmount").GetDecimal());
        Assert.Equal(0,voidSaved.GetProperty("unappliedBalance").GetDecimal());
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(path,voidRequest,ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path,new {kind="void",voidRequest.effectiveDate,voidRequest.reason,idempotencyKey="again",voidRequest.allocations},ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/gestion-logial/tesoreria/recibos/{receiptId}/imputaciones",new {allocations=new[]{new {chargeId=targetId,amount=100m}}},ct)).StatusCode);
        var oldReport=await client.GetFromJsonAsync<JsonElement>($"/api/gestion-logial/tesoreria/talleres/{organizationId}/reportes?from=2025-01-01&to=2025-12-31",ct);
        Assert.Equal(20000,oldReport.GetProperty("income").GetDecimal());
        var report=await client.GetFromJsonAsync<JsonElement>($"/api/gestion-logial/tesoreria/talleres/{organizationId}/reportes?from=2026-01-01&to=2026-12-31",ct);
        Assert.Equal(-20000,report.GetProperty("income").GetDecimal());
        Assert.Equal(0,report.GetProperty("closingBalance").GetDecimal());
        Assert.Equal("ingreso",report.GetProperty("movements").EnumerateArray().Single().GetProperty("type").GetString());
        await using var verify=factory.Services.CreateAsyncScope();
        var verifyDb=verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var original=await verifyDb.LodgeMemberReceipts.Include(x=>x.Allocations).SingleAsync(x=>x.Id==receiptId,ct);
        Assert.Equal(20000,original.Amount); Assert.Equal(new DateOnly(2025,12,20),original.PaymentDate);
        Assert.Equal(7000,original.Allocations.Single(x=>x.Id==allocationId).Amount);
        Assert.Equal(0,original.Allocations.Sum(x=>x.Amount));
        Assert.Equal(2,await verifyDb.LodgeReceiptAdjustments.CountAsync(x=>x.ReceiptId==receiptId,ct));
        Assert.Equal(20000,(await verifyDb.LodgeTreasuryYearClosures.SingleAsync(x=>x.OrganizationId==organizationId,ct)).ClosingBalance);
        Assert.Equal(20000,(await verifyDb.LodgeTreasuryReconciliations.SingleAsync(x=>x.OrganizationId==organizationId,ct)).ClosingBalance);
        original.Amount=1;
        await Assert.ThrowsAsync<DbUpdateException>(()=>verifyDb.SaveChangesAsync(ct));
        // Exercise the database protections directly, beyond tracked-entity updates.
        var allocationDelete = await Assert.ThrowsAsync<PostgresException>(() => verifyDb.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.lodge_member_payment_allocations WHERE \"Id\" = {allocationId}", ct));
        Assert.Equal("P0001", allocationDelete.SqlState);
        var adjustmentId = saved.GetProperty("id").GetGuid();
        var adjustmentDelete = await Assert.ThrowsAsync<PostgresException>(() => verifyDb.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.lodge_receipt_adjustments WHERE \"Id\" = {adjustmentId}", ct));
        Assert.Equal("P0001", adjustmentDelete.SqlState);
        var receiptDelete = await Assert.ThrowsAsync<PostgresException>(() => verifyDb.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM core.lodge_member_receipts WHERE \"Id\" = {receiptId}", ct));
        Assert.Equal("P0001", receiptDelete.SqlState);
        Assert.Equal(2, await verifyDb.LodgeReceiptAdjustments.CountAsync(x => x.ReceiptId == receiptId, ct));
    }
}
