using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class LodgeTreasuryReconciliationPostgreSqlTests
{
    [Fact]
    public async Task Saves_immutable_reconciliations_calculated_from_ledger_and_returns_history()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var ct = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();
        Guid organizationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            await db.Database.MigrateAsync(ct);
            var organization = new Organization { Name = $"Conciliación {Guid.NewGuid():N}", Number = "CONC-CI", Type = "workshop" };
            organizationId = organization.Id;
            db.Organizations.Add(organization);
            db.LodgeTreasuryConfigurations.Add(new LodgeTreasuryConfiguration
            {
                Organization = organization, OrganizationId = organizationId,
                OpeningBalance = 1_000m, OpeningBalanceDate = new DateOnly(2026, 1, 1),
                IncomeCategories = "Otros ingresos", ExpenseCategories = "Servicios"
            });
            db.LodgeTreasuryIncomes.Add(new LodgeTreasuryIncome
            {
                Organization = organization, OrganizationId = organizationId, Category = "Donación", Amount = 300m,
                IncomeDate = new DateOnly(2026, 1, 10), Description = "Ingreso de prueba", RecordedBySubject = "ci-seed"
            });
            db.LodgeTreasuryExpenses.AddRange(
                new LodgeTreasuryExpense { Organization = organization, OrganizationId = organizationId,
                    Category = "Servicios", Amount = 400m, ExpenseDate = new DateOnly(2026, 1, 11), Description = "Egreso aprobado",
                    ApprovalStatus = "approved", RecordedBySubject = "ci-seed", ApprovedBySubject = "ci-approver", ApprovedAtUtc = DateTimeOffset.UtcNow },
                new LodgeTreasuryExpense { Organization = organization, OrganizationId = organizationId,
                    Category = "Servicios", Amount = 100m, ExpenseDate = new DateOnly(2026, 1, 12), Description = "Egreso pendiente",
                    ApprovalStatus = "pending_approval", RecordedBySubject = "ci-seed" });
            await db.SaveChangesAsync(ct);
        }

        async Task<JsonElement> Record(decimal observed)
        {
            var response = await client.PostAsJsonAsync($"/api/gestion-logial/tesoreria/talleres/{organizationId}/conciliaciones", new
            {
                from = new DateOnly(2026, 1, 1), to = new DateOnly(2026, 1, 31), observedBalance = observed,
                evidenceReference = "ARQUEO-CI", notes = "Dato ficticio para prueba"
            }, ct);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        }

        var first = await Record(950m);
        var second = await Record(900m);
        Assert.NotEqual(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal(1_000m, first.GetProperty("openingBalance").GetDecimal());
        Assert.Equal(300m, first.GetProperty("income").GetDecimal());
        Assert.Equal(400m, first.GetProperty("authorizedExpenses").GetDecimal());
        Assert.Equal(100m, first.GetProperty("pendingExpenses").GetDecimal());
        Assert.Equal(900m, first.GetProperty("closingBalance").GetDecimal());
        Assert.Equal(50m, first.GetProperty("difference").GetDecimal());
        Assert.Equal(3, first.GetProperty("movementCount").GetInt32());
        Assert.Equal("ci-http-admin", first.GetProperty("recordedBySubject").GetString());

        var report = await client.GetFromJsonAsync<JsonElement>(
            $"/api/gestion-logial/tesoreria/talleres/{organizationId}/reportes?from=2026-01-01&to=2026-01-31", ct);
        Assert.Equal(2, report.GetProperty("reconciliationHistory").GetArrayLength());
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        Assert.Equal(2, await verifyDb.LodgeTreasuryReconciliations.CountAsync(x => x.OrganizationId == organizationId, ct));
        Assert.Equal(1, await verifyDb.LodgeTreasuryIncomes.CountAsync(x => x.OrganizationId == organizationId, ct));
        Assert.Equal(2, await verifyDb.LodgeTreasuryExpenses.CountAsync(x => x.OrganizationId == organizationId, ct));
    }
}
