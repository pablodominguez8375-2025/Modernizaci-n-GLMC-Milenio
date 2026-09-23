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
            var pastActivePerson = new Person { FirstNames = "Hermano", LastNames = "Past Activo" };
            var pastActiveMember = new Member { Person = pastActivePerson, PersonId = pastActivePerson.Id, InstitutionalNumber = $"PAST-{Guid.NewGuid():N}" };
            var pastActiveMembership = new Membership
            {
                Member = pastActiveMember, MemberId = pastActiveMember.Id, Organization = organization, OrganizationId = organization.Id,
                MembershipType = GrandTreasuryFeeSchedule.PastActiveMembershipType, StartDate = new DateOnly(2026, 1, 1), Status = MembershipCodes.MembershipStatus.Active
            };
            var degree = new DegreeEvent
            {
                Member = member, MemberId = member.Id, Organization = organization, OrganizationId = organization.Id,
                Degree = TreasuryCodes.Degree.Master, EventType = MembershipCodes.DegreeEvent.Exaltation,
                EffectiveDate = new DateOnly(2025, 6, 1), EvidenceReference = "ACTA-EXALTACION-CI"
            };
            var office = new OfficeAssignment
            {
                Member = member, MemberId = member.Id, Organization = organization, OrganizationId = organization.Id,
                OfficeType = "treasurer", Period = "2026", StartDate = new DateOnly(2026, 1, 1)
            };
            var feePlan = new PMGM.Api.Modules.Treasury.Entities.LodgeFeePlan
            {
                Organization = organization, OrganizationId = organization.Id,
                FeeType = TreasuryCodes.LodgeFeeType.Student,
                MemberAmount = 8_000m, GrandTreasuryAmount = 8_000m,
                EffectiveFrom = new DateOnly(2026, 1, 1)
            };
            var charge = new PMGM.Api.Modules.Treasury.Entities.LodgeMemberCharge
            {
                Organization = organization, OrganizationId = organization.Id,
                Member = member, MemberId = member.Id,
                FeePlan = feePlan, FeePlanId = feePlan.Id,
                PeriodYear = 2026, PeriodMonth = 7,
                MemberAmount = 8_000m, GrandTreasuryAmount = 8_000m,
                Status = TreasuryCodes.LodgeChargeStatus.Pending
            };
            var adjustment = new PMGM.Api.Modules.Treasury.Entities.TreasuryAdjustment
            {
                Member = member, MemberId = member.Id, Organization = organization, OrganizationId = organization.Id,
                AdjustmentType = "student", EffectiveFrom = new DateOnly(2026, 1, 1),
                EffectiveUntil = new DateOnly(2026, 12, 31), Amount = 0m,
                AuthorizationReference = "PLANCHA-CI-001", Status = TreasuryCodes.AdjustmentStatus.Active
            };
            db.AddRange(organization, person, member, membership, pastActivePerson, pastActiveMember, pastActiveMembership, degree, office, feePlan, charge, adjustment);
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

        var line = await client.PostAsJsonAsync($"/api/tesoreria/cuadros/{statementId}/generar-lineas", new
        {
            apprenticeAmount = 21_000m, fellowcraftAmount = 21_000m, masterAmount = 21_000m
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, line.StatusCode);
        var generated = await line.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(8_000m, generated.GetProperty("expectedAmount").GetDecimal());
        var generatedLine = Assert.Single(generated.GetProperty("lines").EnumerateArray());
        Assert.Equal(memberId, generatedLine.GetProperty("memberId").GetGuid());
        Assert.Equal(membershipId, generatedLine.GetProperty("membershipId").GetGuid());
        Assert.Equal("treasurer", generatedLine.GetProperty("officeCodeAtCutoff").GetString());
        Assert.Equal("PLANCHA-CI-001", generatedLine.GetProperty("authorizationReference").GetString());
        Assert.Equal(1, generated.GetProperty("lines").GetArrayLength()); // Past Activo is not assessed by Gran Tesorería.
        var breakdown = Assert.Single(generated.GetProperty("feeBreakdown").EnumerateArray());
        Assert.Equal(TreasuryCodes.LodgeFeeType.Student, breakdown.GetProperty("feeType").GetString());
        Assert.Equal(1, breakdown.GetProperty("members").GetInt32());
        Assert.Equal(8_000m, breakdown.GetProperty("amount").GetDecimal());

        var minimizedResponse = await client.GetAsync($"/api/tesoreria/cuadros/{statementId}", cancellationToken);
        var minimized = await minimizedResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var minimizedLine = Assert.Single(minimized.GetProperty("lines").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, minimizedLine.GetProperty("memberId").ValueKind);
        Assert.Equal(JsonValueKind.Null, minimizedLine.GetProperty("observation").ValueKind);

        var detailedResponse = await client.GetAsync($"/api/tesoreria/cuadros/{statementId}?includeMemberDetail=true", cancellationToken);
        var detailed = await detailedResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(memberId, Assert.Single(detailed.GetProperty("lines").EnumerateArray()).GetProperty("memberId").GetGuid());

        var blockedSubmit = await client.PostAsync($"/api/tesoreria/cuadros/{statementId}/enviar", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, blockedSubmit.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/tesoreria/cuadros/{statementId}/pagos", new
        {
            paymentMethod = TreasuryCodes.PaymentMethod.Transfer, paymentDate = new DateOnly(2026, 7, 10),
            amount = 7000m, payerDisplayName = "Tesorero", payerRut = (string?)null, reference = "TRX-1"
        }, cancellationToken)).StatusCode);

        var stillBlockedSubmit = await client.PostAsync($"/api/tesoreria/cuadros/{statementId}/enviar", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, stillBlockedSubmit.StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/tesoreria/cuadros/{statementId}/pagos", new
        {
            paymentMethod = TreasuryCodes.PaymentMethod.Deposit, paymentDate = new DateOnly(2026, 7, 10),
            amount = 1000m, payerDisplayName = "Tesorero", payerRut = (string?)null, reference = "DEP-1"
        }, cancellationToken)).StatusCode);

        var list = await client.GetAsync($"/api/tesoreria/talleres/{organizationId}/cuadros?year=2026&month=7", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listJson = await list.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(statementId, Assert.Single(listJson.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/tesoreria/cuadros/{statementId}/enviar", null, cancellationToken)).StatusCode);

        var paymentAfterSubmit = await client.PostAsJsonAsync($"/api/tesoreria/cuadros/{statementId}/pagos", new
        {
            paymentMethod = TreasuryCodes.PaymentMethod.Transfer, paymentDate = new DateOnly(2026, 7, 10),
            amount = 1m, payerDisplayName = "Tesorero", payerRut = (string?)null, reference = "NO-DEBE-ACEPTAR"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, paymentAfterSubmit.StatusCode);

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
