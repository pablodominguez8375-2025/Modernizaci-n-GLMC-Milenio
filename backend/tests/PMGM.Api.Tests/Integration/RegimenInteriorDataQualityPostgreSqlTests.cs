using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class RegimenInteriorDataQualityPostgreSqlTests
{
    [Fact]
    public async Task Detects_impossible_degree_sequence_and_activity_after_death()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new PmgmDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var workshop = new Organization
        {
            Name = $"Calidad Taller {suffix}",
            Number = $"Q{suffix[..3]}",
            Type = "workshop"
        };
        var person = new Person { FirstNames = "Hermana", LastNames = $"Calidad {suffix}" };
        var member = new Member { Person = person, InstitutionalNumber = $"DQ-{suffix}" };
        var membership = new Membership
        {
            Member = member,
            Organization = workshop,
            MembershipType = "regular",
            StartDate = new DateOnly(2018, 1, 1),
            Status = MembershipCodes.MembershipStatus.Active
        };

        db.AddRange(workshop, person, member, membership);
        db.DegreeEvents.AddRange(
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
            },
            new DegreeEvent
            {
                Member = member,
                Organization = workshop,
                Degree = "master",
                EventType = MembershipCodes.DegreeEvent.Exaltation,
                EffectiveDate = new DateOnly(2018, 2, 1)
            });
        db.InstitutionalStatusEvents.Add(new InstitutionalStatusEvent
        {
            Member = member,
            Organization = workshop,
            EventType = MembershipCodes.InstitutionalStatus.Deceased,
            EffectiveDate = new DateOnly(2025, 6, 1)
        });
        db.OfficeAssignments.Add(new OfficeAssignment
        {
            Member = member,
            Organization = workshop,
            OfficeType = "secretary",
            Period = "2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31)
        });
        await db.SaveChangesAsync(cancellationToken);

        var service = new RegimenInteriorDataQualityService(db);
        var response = await service.QueryAsync(new DataQualityQuery(
            new DateOnly(2026, 9, 9),
            workshop.Id,
            null,
            null,
            suffix,
            100), cancellationToken);

        Assert.True(response.Summary.Errors >= 4);
        Assert.Equal(1, response.Summary.AffectedMembers);
        Assert.Contains(response.Items, x => x.Code == "wage_increase_before_initiation" && x.Severity == DataQualitySeverity.Error);
        Assert.Contains(response.Items, x => x.Code == "exaltation_before_wage_increase" && x.Severity == DataQualitySeverity.Error);
        Assert.Contains(response.Items, x => x.Code == "office_after_death" && x.Severity == DataQualitySeverity.Error);
        Assert.Contains(response.Items, x => x.Code == "active_membership_after_death" && x.Severity == DataQualitySeverity.Error);

        var filtered = await service.QueryAsync(new DataQualityQuery(
            new DateOnly(2026, 9, 9),
            workshop.Id,
            DataQualitySeverity.Error,
            "office_after_death",
            suffix,
            10), cancellationToken);

        var issue = Assert.Single(filtered.Items);
        Assert.Equal(member.Id, issue.MemberId);
        Assert.Equal(workshop.Id, issue.OrganizationId);
        Assert.Equal(new DateOnly(2026, 1, 1), issue.PrimaryDate);
        Assert.Equal(new DateOnly(2025, 6, 1), issue.RelatedDate);
    }
}
