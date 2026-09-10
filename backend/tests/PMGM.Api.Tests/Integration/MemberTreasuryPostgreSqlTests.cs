using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class MemberTreasuryPostgreSqlTests
{
    [Fact]
    public async Task Treasury_ledger_migration_and_partial_payment_projection_work_on_postgresql()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var pmgmOptions = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var ledgerOptions = new DbContextOptionsBuilder<TreasuryLedgerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(pmgmOptions);
        await db.Database.MigrateAsync(cancellationToken);

        await using var ledger = new TreasuryLedgerDbContext(ledgerOptions);
        await ledger.Database.MigrateAsync(cancellationToken);

        var applied = (await ledger.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260910220000_AddMemberTreasuryLedger", applied);

        var organization = new Organization
        {
            Name = $"Taller QA Tesorería {Guid.NewGuid():N}",
            Number = $"QA-{Random.Shared.Next(10000, 99999)}",
            Type = "workshop"
        };
        var person = new Person
        {
            FirstNames = "Hermano",
            LastNames = "QA Tesorería"
        };
        var member = new Member
        {
            PersonId = person.Id,
            InstitutionalNumber = $"QA-TES-{Guid.NewGuid():N}"
        };
        var membership = new Membership
        {
            MemberId = member.Id,
            OrganizationId = organization.Id,
            MembershipType = "regular",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
            Status = "active"
        };

        db.Organizations.Add(organization);
        db.People.Add(person);
        db.Members.Add(member);
        db.Memberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);

        var futureDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var olderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var partialCharge = new MemberCharge
        {
            OrganizationId = organization.Id,
            MemberId = member.Id,
            Concept = "Cargo QA parcial",
            Period = "QA",
            IssuedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = futureDueDate,
            Amount = 100_000m,
            Currency = MemberTreasuryCodes.Currency.Clp,
            Status = MemberTreasuryCodes.ChargeStatus.Open,
            CreatedBySubject = "qa-treasurer"
        };
        var paidCharge = new MemberCharge
        {
            OrganizationId = organization.Id,
            MemberId = member.Id,
            Concept = "Cargo QA pagado",
            Period = "QA anterior",
            IssuedDate = olderDate,
            DueDate = olderDate,
            Amount = 50_000m,
            Currency = MemberTreasuryCodes.Currency.Clp,
            Status = MemberTreasuryCodes.ChargeStatus.Paid,
            CreatedBySubject = "qa-treasurer"
        };
        var partialPayment = new MemberPayment
        {
            OrganizationId = organization.Id,
            MemberId = member.Id,
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = 40_000m,
            Currency = MemberTreasuryCodes.Currency.Clp,
            Method = MemberTreasuryCodes.PaymentMethod.Transfer,
            ReceiptNumber = "QA-REC-PARTIAL",
            RecordedBySubject = "qa-treasurer"
        };
        var completePayment = new MemberPayment
        {
            OrganizationId = organization.Id,
            MemberId = member.Id,
            PaymentDate = olderDate,
            Amount = 50_000m,
            Currency = MemberTreasuryCodes.Currency.Clp,
            Method = MemberTreasuryCodes.PaymentMethod.Transfer,
            ReceiptNumber = "QA-REC-PAID",
            RecordedBySubject = "qa-treasurer"
        };

        ledger.MemberCharges.AddRange(partialCharge, paidCharge);
        ledger.MemberPayments.AddRange(partialPayment, completePayment);
        ledger.MemberPaymentAllocations.AddRange(
            new MemberPaymentAllocation
            {
                PaymentId = partialPayment.Id,
                ChargeId = partialCharge.Id,
                Amount = 40_000m
            },
            new MemberPaymentAllocation
            {
                PaymentId = completePayment.Id,
                ChargeId = paidCharge.Id,
                Amount = 50_000m
            });
        await ledger.SaveChangesAsync(cancellationToken);

        var statement = await MemberTreasuryStatementProjection.BuildAsync(
            ledger,
            organization.Id,
            member.Id,
            cancellationToken);

        Assert.Equal("CLP", statement.Currency);
        Assert.Equal(150_000m, statement.Summary.TotalCharges);
        Assert.Equal(90_000m, statement.Summary.TotalPayments);
        Assert.Equal(90_000m, statement.Summary.TotalAllocated);
        Assert.Equal(60_000m, statement.Summary.OutstandingCharges);
        Assert.Equal(0m, statement.Summary.AvailableCredit);
        Assert.Equal(60_000m, statement.Summary.NetBalance);
        Assert.Equal(1, statement.Summary.PendingChargeCount);
        Assert.Equal(0, statement.Summary.OverdueChargeCount);

        var projectedPartial = Assert.Single(statement.Charges.Where(x => x.Id == partialCharge.Id));
        Assert.Equal(40_000m, projectedPartial.AppliedAmount);
        Assert.Equal(60_000m, projectedPartial.OutstandingAmount);

        var projectedPaid = Assert.Single(statement.Charges.Where(x => x.Id == paidCharge.Id));
        Assert.Equal(0m, projectedPaid.OutstandingAmount);

        var projectedPartialPayment = Assert.Single(statement.Payments.Where(x => x.Id == partialPayment.Id));
        Assert.Equal(0m, projectedPartialPayment.UnappliedAmount);
        Assert.False(projectedPartialPayment.ReceiptAvailable);
    }
}
