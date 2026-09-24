using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LodgePaymentIdempotencyPostgreSqlTests
{
    [Fact]
    public async Task Retrying_same_payment_returns_original_receipt_without_duplicate_ledger_entry()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        Guid chargeId;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            var organization = new Organization { Name = $"Taller idempotencia {Guid.NewGuid():N}", Number = $"IDEM-{Guid.NewGuid():N}", Type = "workshop" };
            var person = new Person { FirstNames = "Hermano", LastNames = "Idempotencia", Rut = $"IDEM-{Guid.NewGuid():N}" };
            var member = new Member { Person = person, PersonId = person.Id, InstitutionalNumber = $"IDEM-{Guid.NewGuid():N}" };
            var plan = new LodgeFeePlan { Organization = organization, OrganizationId = organization.Id, FeeType = TreasuryCodes.LodgeFeeType.Normal,
                MemberAmount = 5000m, GrandTreasuryAmount = 5000m, EffectiveFrom = new DateOnly(2026, 1, 1) };
            var charge = new LodgeMemberCharge { Organization = organization, OrganizationId = organization.Id, Member = member, MemberId = member.Id,
                FeePlan = plan, FeePlanId = plan.Id, PeriodYear = 2026, PeriodMonth = 9, MemberAmount = 5000m,
                GrandTreasuryAmount = 5000m, Status = TreasuryCodes.LodgeChargeStatus.Pending };
            db.AddRange(organization, person, member, plan, charge);
            await db.SaveChangesAsync(cancellationToken);
            chargeId = charge.Id;
        }

        var request = new { amount = 2500m, paymentMethod = "cash", paymentDate = new DateOnly(2026, 9, 24), reference = (string?)null,
            idempotencyKey = $"retry-{Guid.NewGuid():N}" };
        var path = $"/api/gestion-logial/tesoreria/cargos/{chargeId}/pagos";
        var first = await client.PostAsJsonAsync(path, request, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync(cancellationToken));
        var receipt = firstBody.RootElement.GetProperty("receiptNumber").GetString();

        var retry = await client.PostAsJsonAsync(path, request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var retryBody = JsonDocument.Parse(await retry.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(receipt, retryBody.RootElement.GetProperty("receiptNumber").GetString());
        Assert.Equal(2500m, retryBody.RootElement.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(2500m, retryBody.RootElement.GetProperty("balance").GetDecimal());

        var changedPayload = await client.PostAsJsonAsync(path, new { amount = 3000m, request.paymentMethod, request.paymentDate,
            request.reference, request.idempotencyKey }, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, changedPayload.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(1, await verificationDb.LodgeMemberPayments.CountAsync(x => x.ChargeId == chargeId, cancellationToken));
    }
}
