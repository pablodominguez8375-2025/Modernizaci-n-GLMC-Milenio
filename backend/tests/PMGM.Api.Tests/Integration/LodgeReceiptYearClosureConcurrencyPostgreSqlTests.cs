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
public sealed class LodgeReceiptYearClosureConcurrencyPostgreSqlTests
{
    [Theory]
    [InlineData("void", "CLP", true)]
    [InlineData("void", "CLP", false)]
    [InlineData("correction", "CLP", true)]
    [InlineData("correction", "CLP", false)]
    [InlineData("void", "USD", true)]
    [InlineData("void", "USD", false)]
    [InlineData("correction", "USD", true)]
    [InlineData("correction", "USD", false)]
    public async Task Adjustment_and_year_closure_preserve_serializable_cash_history_and_retries(string kind, string currency, bool adjustmentCommitsFirst)
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        var gate = new FirstClosureReadGate();
        using var originalFactory = new PmgmWebApplicationFactory(connection);
        using var factory = originalFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<PmgmDbContext>(options => options.AddInterceptors(gate))));
        using var client = factory.CreateClient();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Santiago").DateTime);
        var year = today.Year - 1;
        var paymentDate = new DateOnly(year, 9, 20);
        var effectiveDate = paymentDate.AddDays(1);
        Guid receiptId, allocationId, organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var org = new Organization { Name = "Cierre concurrente ficticio", Number = $"CLOSE-{Guid.NewGuid():N}", Type = "workshop" };
            var member = new Member { Person = new Person { FirstNames = "Hermano", LastNames = "Ficticio" }, InstitutionalNumber = $"CLOSE-{Guid.NewGuid():N}" };
            var plan = new LodgeFeePlan { Organization = org, FeeType = "normal", Currency = currency, MemberAmount = 10000, GrandTreasuryAmount = 5000, EffectiveFrom = new DateOnly(year, 1, 1) };
            var charge = new LodgeMemberCharge { Organization = org, Member = member, FeePlan = plan, Currency = currency, PeriodYear = year, PeriodMonth = 9, MemberAmount = 10000, GrandTreasuryAmount = 5000, Status = "pending" };
            var receipt = new LodgeMemberReceipt { OrganizationId = org.Id, Member = member, MemberId = member.Id, Currency = currency, Amount = 20000, PaymentMethod = "transfer", PaymentDate = paymentDate, ReceiptNumber = $"CI-{Guid.NewGuid():N}", IdempotencyKey = Guid.NewGuid().ToString(), RecordedBySubject = "ci-seed" };
            var allocation = new LodgeMemberPaymentAllocation { Receipt = receipt, Charge = charge, Amount = 7000, AllocatedBySubject = "ci-seed" };
            db.AddRange(org, member, plan, charge, receipt, allocation);
            await db.SaveChangesAsync(ct);
            receiptId = receipt.Id; allocationId = allocation.Id; organizationId = org.Id;
        }

        object Adjustment(DateOnly date) => new
        {
            kind, effectiveDate = date, reason = "Ajuste ficticio frente a cierre", idempotencyKey = "year-close-adjustment",
            allocationId = kind == "correction" ? (Guid?)allocationId : null,
            amount = kind == "correction" ? (decimal?)5000 : null, allocations = Array.Empty<object>()
        };
        var adjustmentPath = $"/api/gestion-logial/tesoreria/recibos/{receiptId}/ajustes";
        var closurePath = $"/api/gestion-logial/tesoreria/talleres/{organizationId}/cierres-anuales/{year}/cerrar?currencyCode={currency}";
        // Hold the losing request after its serializable closed-year SELECT has executed.
        // Then let the competing request commit, before releasing the stale snapshot.
        // Both orders are forced; there are no scheduler-dependent sleeps or retry loops.
        var heldRequest = adjustmentCommitsFirst
            ? client.PostAsync(closurePath, null, ct)
            : client.PostAsJsonAsync(adjustmentPath, Adjustment(effectiveDate), ct);
        HttpResponseMessage committed;
        try
        {
            await gate.Observed.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            committed = adjustmentCommitsFirst
                ? await client.PostAsJsonAsync(adjustmentPath, Adjustment(effectiveDate), ct)
                : await client.PostAsync(closurePath, null, ct);
        }
        finally { gate.Release.TrySetResult(); }
        var held = await heldRequest;
        Assert.Equal(1, gate.Arrivals);
        Assert.Equal(HttpStatusCode.Created, committed.StatusCode);
        // A cash-neutral correction may serialize before the closure and both may commit.
        // A void changes the closure's cash projection, so its stale competitor must abort.
        if (kind == "void") Assert.Equal(HttpStatusCode.Conflict, held.StatusCode);
        else Assert.Contains(held.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        var adjustmentSucceeded = adjustmentCommitsFirst || held.IsSuccessStatusCode;
        var closureSucceeded = !adjustmentCommitsFirst || held.IsSuccessStatusCode;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            Assert.Equal(adjustmentSucceeded ? 1 : 0, await db.LodgeReceiptAdjustments.CountAsync(x => x.ReceiptId == receiptId, ct));
            Assert.Equal(closureSucceeded ? 1 : 0, await db.LodgeTreasuryYearClosures.CountAsync(x => x.OrganizationId == organizationId, ct));
            Assert.Equal(adjustmentSucceeded ? 1 : 0, await db.AuditEvents.CountAsync(x => x.OrganizationId == organizationId && x.Action == "lodge.treasury.member_receipt.adjusted", ct));
            Assert.Equal(closureSucceeded ? 1 : 0, await db.AuditEvents.CountAsync(x => x.OrganizationId == organizationId && x.Action == "lodge.treasury.year.closed", ct));
        }
        if (!closureSucceeded)
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(closurePath, null, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(closurePath, null, ct)).StatusCode);

        LodgeTreasuryYearClosure snapshot;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            snapshot = await db.LodgeTreasuryYearClosures.AsNoTracking().SingleAsync(x => x.OrganizationId == organizationId, ct);
            var voidBeforeClosure = adjustmentSucceeded && kind == "void";
            Assert.Equal(currency, snapshot.Currency);
            Assert.Equal(year, snapshot.AccountingYear);
            Assert.Equal(0m, snapshot.OpeningBalance);
            Assert.Equal(voidBeforeClosure ? 0m : 20000m, snapshot.Income);
            Assert.Equal(snapshot.Income, snapshot.ClosingBalance);
            Assert.Equal(0m, snapshot.AuthorizedExpenses);
            Assert.Equal(voidBeforeClosure ? 2 : 1, snapshot.MovementCount);
        }
        var finalDate = effectiveDate;
        if (!adjustmentSucceeded)
        {
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(adjustmentPath, Adjustment(effectiveDate), ct)).StatusCode);
            // An explicitly requested current-year adjustment preserves the closed year.
            finalDate = today;
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(adjustmentPath, Adjustment(finalDate), ct)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(adjustmentPath, Adjustment(finalDate), ct)).StatusCode);
        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var saved = await verifyDb.LodgeMemberReceipts.AsNoTracking().Include(x => x.Allocations).Include(x => x.Adjustments).SingleAsync(x => x.Id == receiptId, ct);
        var adjustment = Assert.Single(saved.Adjustments);
        Assert.Equal(20000m, saved.Amount);
        Assert.Equal(paymentDate, saved.PaymentDate);
        Assert.Equal(currency, saved.Currency);
        Assert.Equal(7000m, saved.Allocations.Single(x => x.Id == allocationId).Amount);
        Assert.Equal(finalDate, adjustment.EffectiveDate);
        Assert.Equal(kind == "void" ? 0m : 2000m, saved.Allocations.Sum(x => x.Amount));
        Assert.Equal(kind == "void" ? 0m : 18000m, saved.Amount + adjustment.CashAmount - saved.Allocations.Sum(x => x.Amount));
        var finalClosure = await verifyDb.LodgeTreasuryYearClosures.AsNoTracking().SingleAsync(x => x.Id == snapshot.Id, ct);
        Assert.Equal(snapshot.Income, finalClosure.Income);
        Assert.Equal(snapshot.ClosingBalance, finalClosure.ClosingBalance);
        Assert.Equal(snapshot.MovementCount, finalClosure.MovementCount);
        Assert.Equal(snapshot.ClosedAtUtc, finalClosure.ClosedAtUtc);
        Assert.Equal("ci-http-admin", finalClosure.ClosedBySubject);
        var audits = await verifyDb.AuditEvents.Where(x => x.OrganizationId == organizationId &&
            (x.Action == "lodge.treasury.member_receipt.adjusted" || x.Action == "lodge.treasury.year.closed")).ToListAsync(ct);
        Assert.Equal(2, audits.Count);
        Assert.All(audits, x => { Assert.Equal("success", x.Result); Assert.Equal("ci-http-admin", x.ActorSubject); });
    }

    private sealed class FirstClosureReadGate : DbCommandInterceptor
    {
        public TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public int Arrivals => Volatile.Read(ref arrivals);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.Transaction?.IsolationLevel == IsolationLevel.Serializable &&
                command.CommandText.Contains("lodge_treasury_year_closures", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref arrivals, 1, 0) == 0)
            {
                Observed.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
