using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
public sealed class LodgeReceiptConcurrencyPostgreSqlTests
{
    [Theory]
    [InlineData("void", false)]
    [InlineData("void", true)]
    [InlineData("correction", false)]
    public async Task Competing_adjustments_commit_once_and_preserve_balances_and_audit(string kind, bool sameKey)
    {
        var connection = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var ct = TestContext.Current.CancellationToken;
        var snapshots = new ReceiptSnapshotBarrier();
        using var originalFactory = new PmgmWebApplicationFactory(connection);
        using var factory = originalFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<PmgmDbContext>(options => options.AddInterceptors(snapshots))));
        using var client = factory.CreateClient();
        Guid receiptId, allocationId, organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var org = new Organization { Name = "Concurrent receipts CI", Number = $"RACE-{Guid.NewGuid():N}", Type = "workshop" };
            var member = new Member { Person = new Person { FirstNames = "Hermano", LastNames = "Ficticio" }, InstitutionalNumber = $"RACE-{Guid.NewGuid():N}" };
            var plan = new LodgeFeePlan { Organization = org, FeeType = "normal", MemberAmount = 10000, GrandTreasuryAmount = 5000, EffectiveFrom = new DateOnly(2026, 1, 1) };
            var charge = new LodgeMemberCharge { Organization = org, Member = member, FeePlan = plan, PeriodYear = 2026, PeriodMonth = 9, MemberAmount = 10000, GrandTreasuryAmount = 5000, Status = "pending" };
            var receipt = new LodgeMemberReceipt { OrganizationId = org.Id, Member = member, MemberId = member.Id, Amount = 20000, PaymentMethod = "transfer", PaymentDate = new DateOnly(2026, 9, 20), ReceiptNumber = $"CI-{Guid.NewGuid():N}", IdempotencyKey = Guid.NewGuid().ToString(), RecordedBySubject = "ci-seed" };
            var allocation = new LodgeMemberPaymentAllocation { Receipt = receipt, Charge = charge, Amount = 7000, AllocatedBySubject = "ci-seed" };
            db.AddRange(org, member, plan, charge, receipt, allocation);
            await db.SaveChangesAsync(ct);
            receiptId = receipt.Id; allocationId = allocation.Id; organizationId = org.Id;
        }

        object Request(string key) => new
        {
            kind, effectiveDate = new DateOnly(2026, 9, 29), reason = "Corrección de registro ficticio",
            idempotencyKey = key, allocationId = kind == "correction" ? (Guid?)allocationId : null,
            amount = kind == "correction" ? (decimal?)5000 : null, allocations = Array.Empty<object>()
        };
        var path = $"/api/gestion-logial/tesoreria/recibos/{receiptId}/ajustes";
        var requests = new[] { Request("first"), Request(sameKey ? "first" : "second") };
        // Both SELECTs execute inside serializable transactions before either request may write.
        // The race is deliberate, rather than relying on scheduler timing or repeated attempts.
        var responses = await Task.WhenAll(requests.Select(request => client.PostAsJsonAsync(path, request, ct)));
        Assert.Equal(2, snapshots.Arrivals);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var winner = Array.FindIndex(responses, r => r.StatusCode == HttpStatusCode.Created);
        var committed = await responses[winner].Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var replay = await client.PostAsJsonAsync(path, requests[winner], ct);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(committed.GetProperty("id").GetGuid(), (await replay.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct)).GetProperty("id").GetGuid());
        var losingRetry = await client.PostAsJsonAsync(path, requests[1 - winner], ct);
        Assert.Equal(sameKey ? HttpStatusCode.OK : HttpStatusCode.Conflict, losingRetry.StatusCode);

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var saved = await verifyDb.LodgeMemberReceipts.AsNoTracking().Include(x => x.Allocations).Include(x => x.Adjustments).SingleAsync(x => x.Id == receiptId, ct);
        var adjustment = Assert.Single(saved.Adjustments);
        Assert.Equal(7000, saved.Allocations.Single(x => x.Id == allocationId).Amount);
        Assert.Equal(20000, saved.Amount);
        Assert.Equal(new DateOnly(2026, 9, 20), saved.PaymentDate);
        Assert.Equal(kind == "void" ? 0 : 2000, saved.Allocations.Sum(x => x.Amount));
        Assert.Equal(kind == "void" ? -20000 : 0, adjustment.CashAmount);
        Assert.Equal(kind == "void" ? 0 : 18000, saved.Amount + adjustment.CashAmount - saved.Allocations.Sum(x => x.Amount));
        var audit = Assert.Single(await verifyDb.AuditEvents.Where(x => x.OrganizationId == organizationId && x.Action == "lodge.treasury.member_receipt.adjusted").ToListAsync(ct));
        Assert.Equal(adjustment.Id.ToString(), audit.EntityId);
        Assert.Equal("ci-http-admin", audit.ActorSubject);
        Assert.Equal("ci-http-admin", adjustment.RecordedBySubject);
        Assert.Equal("success", audit.Result);
        Assert.NotEqual(default, adjustment.RecordedAtUtc);
        var metadata = JsonDocument.Parse(audit.MetadataJson!).RootElement;
        Assert.Equal(receiptId, metadata.GetProperty("Id").GetGuid());
        Assert.Equal(kind, metadata.GetProperty("Kind").GetString());
        Assert.Equal(adjustment.Reason, metadata.GetProperty("Reason").GetString());
    }

    private sealed class ReceiptSnapshotBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public int Arrivals => Math.Min(Volatile.Read(ref arrivals), 2);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.Transaction?.IsolationLevel == IsolationLevel.Serializable &&
                command.CommandText.Contains("lodge_member_receipts", StringComparison.Ordinal))
            {
                var arrival = Interlocked.Increment(ref arrivals);
                if (arrival == 2) release.TrySetResult();
                if (arrival <= 2) await release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
