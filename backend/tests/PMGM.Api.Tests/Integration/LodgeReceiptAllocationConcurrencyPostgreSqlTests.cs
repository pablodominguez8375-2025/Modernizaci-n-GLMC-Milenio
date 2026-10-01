using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LodgeReceiptAllocationConcurrencyPostgreSqlTests
{
    [Theory]
    [InlineData("void", "CLP")]
    [InlineData("correction", "CLP")]
    [InlineData("allocation", "CLP")]
    [InlineData("void", "USD")]
    [InlineData("correction", "USD")]
    [InlineData("allocation", "USD")]
    public async Task Competing_allocation_and_adjustment_preserve_credit_cash_and_audit(string competingOperation, string currency)
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        var barrier = new AllocationSnapshotBarrier();
        using var originalFactory = new PmgmWebApplicationFactory(connection);
        using var factory = originalFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<PmgmDbContext>(options => options.AddInterceptors(barrier))));
        using var client = factory.CreateClient();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        Guid receiptId, sourceId, allocationId, destinationId, organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var org = new Organization { Name = "Imputaciones concurrentes ficticias", Number = $"ALLOC-{Guid.NewGuid():N}", Type = "workshop" };
            var member = new Member { Person = new Person { FirstNames = "Hermano", LastNames = "Ficticio" }, InstitutionalNumber = $"ALLOC-{Guid.NewGuid():N}" };
            var plan = new LodgeFeePlan { Organization = org, FeeType = "normal", Currency = currency, MemberAmount = 10000, GrandTreasuryAmount = 5000, EffectiveFrom = new DateOnly(today.Year, 1, 1) };
            var source = new LodgeMemberCharge { Organization = org, Member = member, FeePlan = plan, Currency = currency, PeriodYear = today.Year, PeriodMonth = 1, MemberAmount = 10000, GrandTreasuryAmount = 5000, Status = "pending" };
            var destination = new LodgeMemberCharge { Organization = org, Member = member, FeePlan = plan, Currency = currency, PeriodYear = today.Year, PeriodMonth = 2, MemberAmount = 10000, GrandTreasuryAmount = 5000, Status = "pending" };
            var receipt = new LodgeMemberReceipt { OrganizationId = org.Id, Member = member, MemberId = member.Id, Currency = currency, Amount = 20000, PaymentMethod = "transfer", PaymentDate = today.AddDays(-1), ReceiptNumber = $"CI-{Guid.NewGuid():N}", IdempotencyKey = Guid.NewGuid().ToString(), RecordedBySubject = "ci-seed" };
            var allocation = new LodgeMemberPaymentAllocation { Receipt = receipt, Charge = source, Amount = 7000, AllocatedBySubject = "ci-seed" };
            db.AddRange(org, member, plan, source, destination, receipt, allocation);
            await db.SaveChangesAsync(ct);
            receiptId = receipt.Id; sourceId = source.Id; allocationId = allocation.Id; destinationId = destination.Id; organizationId = org.Id;
        }

        var root = $"/api/gestion-logial/tesoreria/recibos/{receiptId}";
        var allocationRequest = new { allocations = new[] { new { chargeId = destinationId, amount = 8000m } } };
        var adjustmentRequest = new
        {
            kind = competingOperation, effectiveDate = today, reason = "Corrección ficticia concurrente", idempotencyKey = "concurrent-adjustment",
            allocationId = competingOperation == "correction" ? (Guid?)allocationId : null,
            amount = competingOperation == "correction" ? (decimal?)5000 : null,
            allocations = competingOperation == "correction" ? new[] { new { chargeId = destinationId, amount = 5000m } } : Array.Empty<object>()
        };
        var competitorPath = competingOperation == "allocation" ? root + "/imputaciones" : root + "/ajustes";
        object competitorRequest = competingOperation == "allocation" ? allocationRequest : adjustmentRequest;
        // Execute both serializable SELECTs before either request writes. No timing-based sleeps.
        var responses = await Task.WhenAll(client.PostAsJsonAsync(competitorPath, competitorRequest, ct),
            client.PostAsJsonAsync(root + "/imputaciones", allocationRequest, ct));
        Assert.Equal(2, barrier.Arrivals);
        var winner = Assert.Single(responses.Select((response, index) => (response, index)), x => x.response.IsSuccessStatusCode);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(winner.index == 0 && competingOperation != "allocation" ? HttpStatusCode.Created : HttpStatusCode.OK, winner.response.StatusCode);

        await using (var verify = factory.Services.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var saved = await db.LodgeMemberReceipts.AsNoTracking().Include(x => x.Allocations).Include(x => x.Adjustments).SingleAsync(x => x.Id == receiptId, ct);
            Assert.Equal(20000m, saved.Amount);
            Assert.Equal(today.AddDays(-1), saved.PaymentDate);
            Assert.Equal(currency, saved.Currency);
            Assert.Equal(7000m, saved.Allocations.Single(x => x.Id == allocationId).Amount);
            var adjustmentWon = winner.index == 0 && competingOperation != "allocation";
            var expectedCash = adjustmentWon && competingOperation == "void" ? 0m : 20000m;
            var expectedAllocated = adjustmentWon ? (competingOperation == "void" ? 0m : 7000m) : 15000m;
            Assert.Equal(expectedCash, saved.Amount + saved.Adjustments.Sum(x => x.CashAmount));
            Assert.Equal(expectedAllocated, saved.Allocations.Sum(x => x.Amount));
            Assert.Equal(expectedCash - expectedAllocated, saved.Amount + saved.Adjustments.Sum(x => x.CashAmount) - saved.Allocations.Sum(x => x.Amount));
            Assert.Equal(adjustmentWon ? 1 : 0, saved.Adjustments.Count);
            Assert.InRange(saved.Allocations.Where(x => x.ChargeId == sourceId).Sum(x => x.Amount), 0m, 10000m);
            Assert.InRange(saved.Allocations.Where(x => x.ChargeId == destinationId).Sum(x => x.Amount), 0m, 10000m);
            var audit = Assert.Single(await db.AuditEvents.Where(x => x.OrganizationId == organizationId &&
                (x.Action == "lodge.treasury.member_receipt.adjusted" || x.Action == "lodge.treasury.member_receipt.allocated")).ToListAsync(ct));
            Assert.Equal("ci-http-admin", audit.ActorSubject);
            Assert.Equal("success", audit.Result);
            Assert.Equal(adjustmentWon ? "lodge.treasury.member_receipt.adjusted" : "lodge.treasury.member_receipt.allocated", audit.Action);
        }

        // Retry only the rejected operation, with its original intent and current database state.
        // If allocation won, a subsequent void is valid and must reverse its new allocation too.
        var loserIndex = 1 - winner.index;
        var retry = loserIndex == 0
            ? await client.PostAsJsonAsync(competitorPath, competitorRequest, ct)
            : await client.PostAsJsonAsync(root + "/imputaciones", allocationRequest, ct);
        var retryVoids = loserIndex == 0 && competingOperation == "void";
        Assert.Equal(retryVoids ? HttpStatusCode.Created : HttpStatusCode.Conflict, retry.StatusCode);
        if (competingOperation != "allocation" && (winner.index == 0 || retryVoids))
        {
            var replay = await client.PostAsJsonAsync(root + "/ajustes", adjustmentRequest, ct);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        }
        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var finalReceipt = await finalDb.LodgeMemberReceipts.AsNoTracking().Include(x => x.Allocations).Include(x => x.Adjustments).SingleAsync(x => x.Id == receiptId, ct);
        Assert.Equal(7000m, finalReceipt.Allocations.Single(x => x.Id == allocationId).Amount);
        Assert.Equal(20000m, finalReceipt.Amount);
        Assert.Equal(retryVoids ? 2 : 1, await finalDb.AuditEvents.CountAsync(x => x.OrganizationId == organizationId &&
            (x.Action == "lodge.treasury.member_receipt.adjusted" || x.Action == "lodge.treasury.member_receipt.allocated"), ct));
        if (competingOperation == "void")
        {
            Assert.Single(finalReceipt.Adjustments);
            Assert.Equal(0m, finalReceipt.Allocations.Sum(x => x.Amount));
            Assert.Equal(0m, finalReceipt.Amount + finalReceipt.Adjustments.Sum(x => x.CashAmount));
        }
    }

    private sealed class AllocationSnapshotBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public int Arrivals => Math.Min(Volatile.Read(ref arrivals), 2);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.Transaction?.IsolationLevel == IsolationLevel.Serializable && command.CommandText.Contains("lodge_member_receipts", StringComparison.Ordinal))
            {
                var arrival = Interlocked.Increment(ref arrivals);
                if (arrival == 2) release.TrySetResult();
                if (arrival <= 2) await release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
