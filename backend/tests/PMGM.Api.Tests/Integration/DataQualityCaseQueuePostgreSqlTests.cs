using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DataQualityCaseQueuePostgreSqlTests
{
    [Fact]
    public async Task Case_queue_deduplicates_claims_resolves_and_allows_new_case_after_resolution()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var coreOptions = new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(connectionString).Options;
        var caseOptions = new DbContextOptionsBuilder<RegimenInteriorDbContext>().UseNpgsql(connectionString).Options;

        await using var coreDb = new PmgmDbContext(coreOptions);
        await coreDb.Database.MigrateAsync(cancellationToken);
        await using var caseDb = new RegimenInteriorDbContext(caseOptions);
        await caseDb.Database.MigrateAsync(cancellationToken);

        var applied = (await caseDb.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260909183600_AddDataQualityCaseQueue", applied);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workshop = new Organization { Name = $"Corroboración {suffix}", Number = $"C{suffix[..3]}", Type = "workshop" };
        var person = new Person { FirstNames = "Hermana", LastNames = $"Caso {suffix}" };
        var member = new Member { Person = person, InstitutionalNumber = $"CASE-{suffix}" };
        var membership = new Membership
        {
            Member = member,
            Organization = workshop,
            MembershipType = "regular",
            StartDate = new DateOnly(2018, 1, 1),
            Status = MembershipCodes.MembershipStatus.Active
        };
        coreDb.AddRange(workshop, person, member, membership);
        coreDb.DegreeEvents.AddRange(
            new DegreeEvent
            {
                Member = member,
                Organization = workshop,
                Degree = "apprentice",
                EventType = MembershipCodes.DegreeEvent.Initiation,
                EffectiveDate = new DateOnly(2020, 3, 1)
            },
            new DegreeEvent
            {
                Member = member,
                Organization = workshop,
                Degree = "fellowcraft",
                EventType = MembershipCodes.DegreeEvent.WageIncrease,
                EffectiveDate = new DateOnly(2019, 2, 1)
            });
        await coreDb.SaveChangesAsync(cancellationToken);

        var qualityService = new RegimenInteriorDataQualityService(coreDb);
        var caseService = new DataQualityCaseService(caseDb, coreDb, qualityService);
        var command = new OpenDataQualityCaseCommand(
            new DateOnly(2026, 9, 9),
            "wage_increase_before_initiation",
            DataQualitySeverity.Error,
            member.Id,
            workshop.Id,
            new DateOnly(2019, 2, 1),
            new DateOnly(2020, 3, 1));
        var reviewer = new CaseActor($"qa-reviewer-{suffix}", "Revisor QA");

        var opened = await caseService.OpenAsync(command, reviewer, cancellationToken);
        Assert.True(opened.Created);
        Assert.Equal(DataQualityCaseCodes.Status.Open, opened.Case.Status);
        Assert.Single(opened.Case.Events);

        var duplicate = await caseService.OpenAsync(command, reviewer, cancellationToken);
        Assert.False(duplicate.Created);
        Assert.Equal(opened.Case.Id, duplicate.Case.Id);

        var claimed = await caseService.ClaimAsync(opened.Case.Id, reviewer, cancellationToken);
        Assert.Equal(DataQualityCaseCodes.Status.UnderReview, claimed.Status);
        Assert.Equal(reviewer.Subject, claimed.AssignedToSubject);
        Assert.Equal(2, claimed.Events.Count);

        await Assert.ThrowsAsync<CaseAlreadyAssignedException>(() =>
            caseService.ClaimAsync(opened.Case.Id, new CaseActor("another-reviewer", "Otro Revisor"), cancellationToken));

        var resolved = await caseService.ResolveAsync(
            opened.Case.Id,
            new ResolveDataQualityCaseCommand(
                DataQualityResolutionOutcome.Confirmed,
                "Fechas corroboradas contra el registro institucional; requiere corrección formal del hito.",
                $"ACTA-QA-{suffix}"),
            reviewer,
            allowOverride: false,
            cancellationToken);
        Assert.Equal(DataQualityCaseCodes.Status.ResolvedConfirmed, resolved.Status);
        Assert.Equal($"ACTA-QA-{suffix}", resolved.EvidenceReference);
        Assert.Equal(3, resolved.Events.Count);
        Assert.NotNull(resolved.ResolvedAtUtc);

        var reopened = await caseService.OpenAsync(command, reviewer, cancellationToken);
        Assert.True(reopened.Created);
        Assert.NotEqual(opened.Case.Id, reopened.Case.Id);
        Assert.Equal(DataQualityCaseCodes.Status.Open, reopened.Case.Status);
    }
}
