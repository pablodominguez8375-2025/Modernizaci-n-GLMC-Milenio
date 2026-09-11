using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class TreasuryOrdinaryDuesEligibilityPostgreSqlTests
{
    [Fact]
    public async Task Past_active_and_sleep_do_not_generate_dues_and_reinstatement_resumes_eligibility()
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
            Name = $"Tesorería Estado {suffix}",
            Number = $"T{suffix[..2]}",
            Type = "workshop"
        };
        var person = new Person
        {
            FirstNames = "Hermano",
            LastNames = $"Tesorería {suffix}"
        };
        var member = new Member
        {
            Person = person,
            InstitutionalNumber = $"TRE-{suffix}"
        };
        var historicalMembership = new Membership
        {
            Member = member,
            Organization = workshop,
            MembershipType = "regular",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 5, 31),
            Status = MembershipCodes.MembershipStatus.Closed,
            EndReason = MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal
        };
        var reinstatedMembership = new Membership
        {
            Member = member,
            Organization = workshop,
            MembershipType = "regular",
            StartDate = new DateOnly(2026, 7, 1),
            Status = MembershipCodes.MembershipStatus.Active
        };

        db.AddRange(workshop, person, member, historicalMembership, reinstatedMembership);
        db.InstitutionalStatusEvents.AddRange(
            new InstitutionalStatusEvent
            {
                Member = member,
                Organization = workshop,
                EventType = MembershipCodes.InstitutionalStatus.Active,
                EffectiveDate = new DateOnly(2026, 1, 1)
            },
            new InstitutionalStatusEvent
            {
                Member = member,
                Organization = workshop,
                EventType = MembershipCodes.InstitutionalStatus.PastActive,
                EffectiveDate = new DateOnly(2026, 2, 1)
            },
            new InstitutionalStatusEvent
            {
                Member = member,
                Organization = workshop,
                EventType = MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal,
                EffectiveDate = new DateOnly(2026, 6, 1)
            },
            new InstitutionalStatusEvent
            {
                Member = member,
                Organization = workshop,
                EventType = MembershipCodes.InstitutionalStatus.Reinstated,
                EffectiveDate = new DateOnly(2026, 7, 1)
            });
        await db.SaveChangesAsync(cancellationToken);

        var service = new TreasuryOrdinaryDuesEligibilityService(db);

        var pastActive = await service.GetAsync(
            workshop.Id,
            member.Id,
            new DateOnly(2026, 3, 1),
            cancellationToken);
        Assert.NotNull(pastActive);
        Assert.Equal(MembershipCodes.InstitutionalStatus.PastActive, pastActive.InstitutionalStatus);
        Assert.True(pastActive.HasMembershipSegment);
        Assert.False(pastActive.GeneratesOrdinaryDues);
        Assert.Equal("past_active_exempt", pastActive.ReasonCode);

        var sleeping = await service.GetAsync(
            workshop.Id,
            member.Id,
            new DateOnly(2026, 6, 15),
            cancellationToken);
        Assert.NotNull(sleeping);
        Assert.Equal(MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal, sleeping.InstitutionalStatus);
        Assert.False(sleeping.HasMembershipSegment);
        Assert.False(sleeping.GeneratesOrdinaryDues);
        Assert.Equal("voluntary_withdrawal_no_active_membership", sleeping.ReasonCode);

        var reinstated = await service.GetAsync(
            workshop.Id,
            member.Id,
            new DateOnly(2026, 7, 15),
            cancellationToken);
        Assert.NotNull(reinstated);
        Assert.Equal(MembershipCodes.InstitutionalStatus.Reinstated, reinstated.InstitutionalStatus);
        Assert.True(reinstated.HasMembershipSegment);
        Assert.True(reinstated.GeneratesOrdinaryDues);
        Assert.Equal("ordinary_dues_applicable", reinstated.ReasonCode);
    }
}
