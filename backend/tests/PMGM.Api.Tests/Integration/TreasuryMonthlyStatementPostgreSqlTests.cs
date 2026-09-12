using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class TreasuryMonthlyStatementPostgreSqlTests
{
    [Fact]
    public async Task Reconciliation_requires_zero_difference_and_emits_regularity_snapshot()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        Guid organizationId;
        Guid memberId;
        Guid membershipId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            var organization = new Organization { Name = $"Taller Tesorería {Guid.NewGuid():N}", Number = "TES-CI", Type = "workshop" };
            var person = new Person { FirstNames = "Hermano", LastNames = "Tesorería" };
            var member = new Member { Person = person, PersonId = person.Id, InstitutionalNumber = $"TES-{Guid.NewGuid():N}" };
            var membership = new Membership
            {
                Member = member, MemberId = member.Id, Organization = organization, OrganizationId = organization.Id,
                MembershipType = "regular", StartDate = new DateOnly(2026, 1, 1), Status = MembershipCodes.MembershipStatus.Active
            };
            db.AddRange(organization, person, member, membership);
            await db.SaveChangesAsync(cancellationToken);
            organizationId = organization.Id;
            memberId = member.Id;
            membershipId = membership.Id;
        }

        var create = await client.PostAsJsonAsync($"/api/tesoreria/talleres/{organizationId}/cuadros", new
        {
            periodYear = 2026, periodMonth = 7, cutoffDate = new DateOnly(2026, 7, 10), sourceReference = "XLSX-CI"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var statementId = (await create.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken)).GetProperty("id").GetGuid();

        var line = await client.PostAsJsonAsync($"/api/tesoreria/cuadros/{statementId}/lineas", new
        {
            memberId, membershipId, degreeCodeAtCutoff = "master",
            officeCodeAtCutoff = "treasurer", baseAmount = 21000m, adjustmentAmount = 0m,
            adjustmentType = (string?)null, authorizationReference = (string?)null,
            observation = (string?)null, identityMatchStatus = TreasuryCodes.IdentityMatchStatus.Matched
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, line.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/tesoreria/cuadros/{statementId}/enviar", null, cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/tesoreria/cuadros/{statementId}/pagos", new
        {
            paymentMethod = TreasuryCodes.PaymentMethod.Transfer, paymentDate = new DateOnly(2026, 7, 10),
            amount = 20000m, payerDisplayName = "Tesorero", payerRut = (string?)null, reference = "TRX-1"
        }, cancellationToken)).StatusCode);

        var blocked = await client.PostAsync($"/api/tesoreria/cuadros/{statementId}/conciliar", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/tesoreria/cuadros/{statementId}/pagos", new
        {
            paymentMethod = TreasuryCodes.PaymentMethod.Deposit, paymentDate = new DateOnly(2026, 7, 10),
            amount = 1000m, payerDisplayName = "Tesorero", payerRut = (string?)null, reference = "DEP-1"
        }, cancellationToken)).StatusCode);
        var reconciled = await client.PostAsync($"/api/tesoreria/cuadros/{statementId}/conciliar", null, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var snapshot = await verificationDb.FinancialRegularitySnapshots.SingleAsync(
            x => x.SourceReference == $"TREASURY-STATEMENT:{statementId}", cancellationToken);
        Assert.Equal(TreasuryCodes.RegularityStatus.UpToDate, snapshot.Status);
        Assert.Contains("treasury.statement.reconciled", await verificationDb.AuditEvents.Select(x => x.Action).ToListAsync(cancellationToken));
    }
}
