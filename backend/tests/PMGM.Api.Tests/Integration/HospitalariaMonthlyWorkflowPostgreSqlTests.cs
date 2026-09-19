using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Hospitalaria;
using PMGM.Api.Modules.LodgeManagement;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class HospitalariaMonthlyWorkflowPostgreSqlTests
{
    [Fact]
    public async Task Workshop_aid_monthly_submission_and_grand_reconciliation_preserve_privacy_boundary()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new PmgmWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        Guid organizationId;
        Guid councilDecisionId;
        Guid councilReviewId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmgmDbContext>();
            var lodgeDb = scope.ServiceProvider.GetRequiredService<LodgeManagementDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var organization = new Organization
            {
                Name = $"Taller Hospitalaria CI {Guid.NewGuid():N}",
                Number = "HOSP-CI",
                Type = "workshop"
            };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync(cancellationToken);
            organizationId = organization.Id;

            var session = new LodgeCouncilSession
            {
                OrganizationId = organizationId,
                SessionDate = new DateOnly(2026, 9, 15),
                Title = "Consejo Hospitalaria CI",
                Status = LodgeCouncilCodes.SessionStatus.Held,
                QualifiedQuorumConfirmed = true,
                QuorumConfirmedBySubject = "ci-council",
                QuorumConfirmedAtUtc = DateTimeOffset.UtcNow,
                CreatedBySubject = "ci-council",
                ClosedAtUtc = DateTimeOffset.UtcNow
            };
            var decision = new LodgeCouncilDecision
            {
                Session = session,
                SessionId = session.Id,
                Category = LodgeCouncilCodes.DecisionCategory.BenevolenceAidProposal,
                Subject = "Socorro reservado CI",
                Resolution = "Se aprueba el socorro conforme al artículo 12.13.",
                Outcome = LodgeCouncilCodes.DecisionOutcome.Approved,
                RequiresChamberReview = false,
                Amount = 45000m,
                RecordedBySubject = "ci-council"
            };
            var review = new LodgeCouncilFinancialReview
            {
                Session = session,
                SessionId = session.Id,
                ControlArea = LodgeCouncilCodes.ControlArea.Hospitalaria,
                PeriodLabel = "2026-09",
                Conclusion = "Estado mensual revisado y conforme.",
                Observations = "Detalle reservado al Consejo del Taller.",
                RecordedBySubject = "ci-council"
            };
            lodgeDb.AddRange(session, decision, review);
            await lodgeDb.SaveChangesAsync(cancellationToken);
            councilDecisionId = decision.Id;
            councilReviewId = review.Id;
        }

        var incomeResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/hospitalaria/talleres/{organizationId}/movimientos",
            new
            {
                movementType = HospitalariaMovementCodes.Income,
                category = HospitalariaMovementCodes.CharityBag,
                amount = 100000m,
                movementDate = new DateOnly(2026, 9, 5),
                memberReference = (string?)null,
                destination = (string?)null,
                evidenceReference = "TENIDA-HOSP-CI-001",
                observation = "Aporte agregado CI"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, incomeResponse.StatusCode);

        var aidResponse = await client.PostAsJsonAsync(
            $"/api/gestion-logial/hospitalaria/talleres/{organizationId}/movimientos",
            new
            {
                movementType = HospitalariaMovementCodes.Expense,
                category = HospitalariaMovementCodes.CharityAid,
                amount = 45000m,
                movementDate = new DateOnly(2026, 9, 10),
                memberReference = "BENEFICIARIO-RESERVADO-CI",
                destination = "SOCORRO-PRIVADO-CI",
                evidenceReference = "AYUDA-HOSP-CI-001",
                observation = "DETALLE-SENSIBLE-NO-DEBE-SALIR-CI"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, aidResponse.StatusCode);
        var aidJson = await aidResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var movementId = aidJson.GetProperty("id").GetGuid();

        var councilApproval = await client.PostAsJsonAsync(
            $"/api/gestion-logial/hospitalaria/movimientos/{movementId}/aprobar-consejo",
            new { councilDecisionId },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, councilApproval.StatusCode);
        var approvalJson = await councilApproval.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(HospitalariaCodes.ApprovalSource.LodgeCouncil, approvalJson.GetProperty("approvalSource").GetString());
        Assert.Equal(councilDecisionId, approvalJson.GetProperty("councilDecisionId").GetGuid());

        var saveSubmission = await client.PutAsJsonAsync(
            $"/api/gestion-logial/hospitalaria/talleres/{organizationId}/rendiciones/2026/9",
            new
            {
                replenishmentDueAmount = 20000m,
                replenishmentPaidAmount = 20000m,
                paymentReference = "TRX-HOSP-CI-001",
                councilFinancialReviewId = councilReviewId,
                sourceReference = "ESTADO-HOSP-CI-2026-09"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, saveSubmission.StatusCode);
        var submissionJson = await saveSubmission.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var submissionId = submissionJson.GetProperty("id").GetGuid();
        Assert.Equal(100000m, submissionJson.GetProperty("incomeAmount").GetDecimal());
        Assert.Equal(45000m, submissionJson.GetProperty("approvedExpenseAmount").GetDecimal());
        Assert.Equal(0m, submissionJson.GetProperty("differenceAmount").GetDecimal());
        Assert.Equal(0, submissionJson.GetProperty("pendingExpenseCount").GetInt32());

        var submitResponse = await client.PostAsync(
            $"/api/gestion-logial/hospitalaria/rendiciones/{submissionId}/enviar",
            null,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var grandQueue = await client.GetAsync(
            $"/api/hospitalaria/rendiciones?organizationId={organizationId}&year=2026&month=9",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, grandQueue.StatusCode);
        var queueText = await grandQueue.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain("BENEFICIARIO-RESERVADO-CI", queueText, StringComparison.Ordinal);
        Assert.DoesNotContain("SOCORRO-PRIVADO-CI", queueText, StringComparison.Ordinal);
        Assert.DoesNotContain("DETALLE-SENSIBLE-NO-DEBE-SALIR-CI", queueText, StringComparison.Ordinal);
        Assert.DoesNotContain("memberReference", queueText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("destination", queueText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("observation", queueText, StringComparison.OrdinalIgnoreCase);

        var queueJson = JsonDocument.Parse(queueText).RootElement;
        var grandItem = Assert.Single(queueJson.GetProperty("items").EnumerateArray());
        Assert.Equal(submissionId, grandItem.GetProperty("id").GetGuid());
        Assert.Equal(20000m, grandItem.GetProperty("replenishmentPaidAmount").GetDecimal());

        var reconcileResponse = await client.PostAsJsonAsync(
            $"/api/hospitalaria/rendiciones/{submissionId}/revision",
            new { decision = HospitalariaCodes.ReviewDecision.Reconciled, notes = "Conciliación CI" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reconcileResponse.StatusCode);

        var regularity = await client.GetAsync(
            $"/api/hospitalaria/talleres/{organizationId}/regularidad?asOf=2026-09-30",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, regularity.StatusCode);
        var regularityJson = await regularity.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        Assert.Equal(HospitalariaCodes.RegularityStatus.UpToDate, regularityJson.GetProperty("status").GetString());
        Assert.Equal("hospitalaria-rendicion:" + submissionId, regularityJson.GetProperty("sourceReference").GetString());

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verifyDb = verificationScope.ServiceProvider.GetRequiredService<PmgmDbContext>();
        var auditActions = await verifyDb.AuditEvents
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => x.Action)
            .ToListAsync(cancellationToken);

        Assert.Contains("lodge.hospitalaria.movement.recorded", auditActions);
        Assert.Contains("lodge.hospitalaria.expense.approved_by_council", auditActions);
        Assert.Contains("lodge.hospitalaria.monthly_submission.saved", auditActions);
        Assert.Contains("lodge.hospitalaria.monthly_submission.submitted", auditActions);
        Assert.Contains("hospitalaria.monthly_submission.reconciled", auditActions);
    }
}
