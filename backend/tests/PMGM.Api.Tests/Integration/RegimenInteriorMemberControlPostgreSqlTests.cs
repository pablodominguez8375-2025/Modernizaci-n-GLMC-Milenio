using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class RegimenInteriorMemberControlPostgreSqlTests
{
    [Fact]
    public async Task Historical_workshop_query_keeps_current_workshop_and_member_milestones()
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
        var source = new Organization { Name = $"Control Origen {suffix}", Number = $"7{suffix[..2]}", Type = "workshop" };
        var target = new Organization { Name = $"Control Destino {suffix}", Number = $"6{suffix[..2]}", Type = "workshop" };
        var person = new Person { FirstNames = "Hermano", LastNames = $"Control {suffix}" };
        var member = new Member { Person = person, InstitutionalNumber = $"CTRL-{suffix}" };
        var sourceMembership = new Membership
        {
            Member = member,
            Organization = source,
            MembershipType = "regular",
            StartDate = new DateOnly(2018, 1, 10),
            EndDate = new DateOnly(2025, 12, 31),
            Status = MembershipCodes.MembershipStatus.Transferred
        };
        var targetMembership = new Membership
        {
            Member = member,
            Organization = target,
            MembershipType = "regular",
            StartDate = new DateOnly(2026, 1, 1),
            Status = MembershipCodes.MembershipStatus.Active
        };

        db.AddRange(source, target, person, member, sourceMembership, targetMembership);
        db.DegreeEvents.AddRange(
            new DegreeEvent { Member = member, Organization = source, Degree = "apprentice", EventType = MembershipCodes.DegreeEvent.Initiation, EffectiveDate = new DateOnly(2018, 2, 1) },
            new DegreeEvent { Member = member, Organization = source, Degree = "fellowcraft", EventType = MembershipCodes.DegreeEvent.WageIncrease, EffectiveDate = new DateOnly(2019, 3, 2) },
            new DegreeEvent { Member = member, Organization = source, Degree = "master", EventType = MembershipCodes.DegreeEvent.Exaltation, EffectiveDate = new DateOnly(2020, 4, 3) });
        db.InstitutionalStatusEvents.AddRange(
            new InstitutionalStatusEvent { Member = member, Organization = source, EventType = MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal, EffectiveDate = new DateOnly(2024, 1, 15) },
            new InstitutionalStatusEvent { Member = member, Organization = source, EventType = MembershipCodes.InstitutionalStatus.Reinstated, EffectiveDate = new DateOnly(2024, 3, 15) });
        db.OfficeAssignments.Add(new OfficeAssignment
        {
            Member = member,
            Organization = source,
            OfficeType = "worshipful_master",
            Period = "2023",
            StartDate = new DateOnly(2023, 1, 1),
            EndDate = new DateOnly(2023, 12, 31)
        });
        db.MemberTransfers.Add(new MemberTransfer
        {
            Member = member,
            SourceMembership = sourceMembership,
            SourceOrganization = source,
            TargetOrganization = target,
            TargetMembership = targetMembership,
            RequestedDate = new DateOnly(2025, 11, 1),
            ProposedEffectiveDate = new DateOnly(2026, 1, 1),
            ApprovedEffectiveDate = new DateOnly(2026, 1, 1),
            Status = MembershipCodes.TransferStatus.Executed,
            ExecutedAtUtc = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero)
        });
        db.FinancialRegularitySnapshots.Add(new FinancialRegularitySnapshot
        {
            Organization = target,
            Member = member,
            Scope = "member",
            Status = "delinquent",
            AsOfDate = new DateOnly(2026, 9, 1)
        });
        await db.SaveChangesAsync(cancellationToken);

        var service = new RegimenInteriorMemberControlService(db);
        var response = await service.QueryAsync(new MemberControlQuery(
            new DateOnly(2026, 9, 9),
            source.Id,
            MembershipCodes.InstitutionalStatus.Reinstated,
            "master",
            "delinquent",
            suffix,
            true,
            false,
            50), cancellationToken);

        var row = Assert.Single(response.Items);
        Assert.Equal("historical", row.Relation);
        Assert.Equal(target.Id, row.CurrentWorkshop?.Id);
        Assert.Equal("master", row.CurrentDegree);
        Assert.Equal(new DateOnly(2018, 2, 1), row.Milestones.Initiation);
        Assert.Equal(new DateOnly(2019, 3, 2), row.Milestones.WageIncrease);
        Assert.Equal(new DateOnly(2020, 4, 3), row.Milestones.Exaltation);
        Assert.Equal(MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal, row.Milestones.WithdrawalType);
        Assert.Equal(new DateOnly(2024, 1, 15), row.Milestones.Withdrawal);
        Assert.Equal(new DateOnly(2024, 3, 15), row.Milestones.Reinstatement);
        Assert.Equal(new DateOnly(2026, 1, 1), row.Milestones.Transfer);
        Assert.Equal("delinquent", row.FinancialStatus);
        Assert.True(row.PastActive);
        Assert.False(row.PendingTransfer);
    }
}
