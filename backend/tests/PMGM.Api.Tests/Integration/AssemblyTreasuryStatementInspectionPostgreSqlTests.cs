using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.Treasury;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyTreasuryStatementInspectionPostgreSqlTests
{
    [Fact]
    public async Task Reads_only_one_lodges_statements_without_certifying_historical_zero()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(ct);

        var workshop = new Organization
        {
            Name = "GT Inspección Taller Ficticio " + Guid.NewGuid().ToString("N")[..8],
            Type = "workshop"
        };
        var other = new Organization
        {
            Name = "GT Inspección Taller Ajeno " + Guid.NewGuid().ToString("N")[..8],
            Type = "workshop"
        };
        static TreasuryMonthlyStatement Certified(Organization org, int month)
        {
            var moment = new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero);
            var row = new TreasuryMonthlyStatement
            {
                Organization = org,
                OrganizationId = org.Id,
                PeriodYear = 2026,
                PeriodMonth = month,
                Currency = "CLP",
                CutoffDate = new DateOnly(2026, month, 18),
                Status = TreasuryCodes.StatementStatus.Reconciled,
                SourceReference = $"GT-INSPECT-2026-{month:00}-{org.Id}",
                BankReceiptConfirmedBySubject = "qa-gran-tesoreria",
                BankReceiptConfirmedAtUtc = moment,
                ReconciledAtUtc = moment
            };
            row.Lines.Add(new TreasuryMonthlyStatementLine
            {
                Statement = row,
                StatementId = row.Id,
                BaseAmount = 1000m,
                DegreeCodeAtCutoff = TreasuryCodes.Degree.Apprentice,
                IdentityMatchStatus = TreasuryCodes.IdentityMatchStatus.Matched
            });
            row.Payments.Add(new TreasuryPayment
            {
                Statement = row,
                StatementId = row.Id,
                PaymentMethod = TreasuryCodes.PaymentMethod.Transfer,
                Amount = 1000m,
                PaymentDate = new DateOnly(2026, month, 17),
                RecordedAtUtc = moment,
                PayerDisplayName = "Taller sintético sin RUT"
            });
            return row;
        }

        db.AddRange(Certified(workshop, 8), Certified(workshop, 9), Certified(workshop, 10),
            Certified(other, 8), Certified(other, 9), Certified(other, 10));
        await db.SaveChangesAsync(ct);

        var inspector = new AssemblyTreasuryStatementInspection(db);
        var cutoff = new DateOnly(2026, 10, 19);
        var result = await inspector.InspectAsync(workshop.Id, new DateOnly(2026, 8, 1), cutoff, ct);
        Assert.Equal(3, result.ObservedPeriods);
        Assert.True(result.ObservedRowsConsistent);
        Assert.Equal(0m, result.ObservedStatementDifference);
        Assert.False(result.MayAuthorizeSuffrage);
        Assert.Contains("HISTORICAL_CERTIFICATION_REQUIRED", result.BlockingReasons);

        // Un período ausente del rango escogido no puede interpretarse como saldo cero.
        var incomplete = await inspector.InspectAsync(workshop.Id,
            new DateOnly(2026, 7, 1), cutoff, ct);
        Assert.Equal(4, incomplete.ExpectedPeriods);
        Assert.Equal(3, incomplete.ObservedPeriods);
        Assert.Contains("MISSING_OR_OUT_OF_SCOPE_PERIOD", incomplete.BlockingReasons);

        // El Taller ajeno no se mezcla ni se expone como parte del expediente.
        var isolated = await inspector.InspectAsync(other.Id,
            new DateOnly(2026, 10, 1), cutoff, ct);
        Assert.Equal(1, isolated.ObservedPeriods);
        Assert.False(isolated.MayAuthorizeSuffrage);
    }
}
