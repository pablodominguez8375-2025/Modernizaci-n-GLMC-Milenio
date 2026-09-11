using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class RegimenInteriorReinstatementMovementPostgreSqlTests
{
    [Fact]
    public async Task Reinstatement_in_another_workshop_reports_source_and_destination_without_overwriting_history()
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
        var source = new Organization
        {
            Name = $"Taller Origen Reintegro {suffix}",
            Number = $"R{suffix[..2]}",
            Type = "workshop"
        };
        var destination = new Organization
        {
            Name = $"Taller Destino Reintegro {suffix}",
            Number = $"D{suffix[..2]}",
            Type = "workshop"
        };
        var person = new Person
        {
            FirstNames = "Hermana",
            LastNames = $"Reintegrada {suffix}"
        };
        var member = new Member
        {
            Person = person,
            InstitutionalNumber = $"REI-{suffix}"
        };
        var sourceMembership = new Membership
        {
            Member = member,
            Organization = source,
            MembershipType = "regular",
            StartDate = new DateOnly(2016, 3, 10),
            EndDate = new DateOnly(2026, 5, 31),
            Status = MembershipCodes.MembershipStatus.Closed,
            EndReason = MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal
        };
        var destinationMembership = new Membership
        {
            Member = member,
            Organization = destination,
            MembershipType = "regular",
            StartDate = new DateOnly(2026, 8, 1),
            Status = MembershipCodes.MembershipStatus.Active
        };

        db.AddRange(source, destination, person, member, sourceMembership, destinationMembership);
        db.InstitutionalStatusEvents.AddRange(
            new InstitutionalStatusEvent
            {
                Member = member,
                Organization = source,
                EventType = MembershipCodes.InstitutionalStatus.VoluntaryWithdrawal,
                EffectiveDate = new DateOnly(2026, 6, 1),
                Reason = "Retiro voluntario de prueba"
            },
            new InstitutionalStatusEvent
            {
                Member = member,
                Organization = destination,
                EventType = MembershipCodes.InstitutionalStatus.Reinstated,
                EffectiveDate = new DateOnly(2026, 8, 1),
                Reason = "Reintegro en otro Taller"
            });
        await db.SaveChangesAsync(cancellationToken);

        var service = new RegimenInteriorMemberControlService(db);
        var response = await service.QueryAsync(new MemberControlQuery(
            new DateOnly(2026, 9, 10),
            null,
            MembershipCodes.InstitutionalStatus.Reinstated,
            null,
            null,
            suffix,
            false,
            false,
            50), cancellationToken);

        var row = Assert.Single(response.Items);
        Assert.Equal(MembershipCodes.InstitutionalStatus.Reinstated, row.CurrentStatus);
        Assert.Equal("current", row.Relation);
        Assert.Equal(destination.Id, row.CurrentWorkshop?.Id);
        Assert.Equal(2, row.MembershipHistoryCount);

        var movement = Assert.IsType<MemberWorkshopMovement>(row.ReinstatementMovement);
        Assert.Equal(source.Id, movement.SourceOrganizationId);
        Assert.Equal(source.Name, movement.SourceOrganizationName);
        Assert.Equal(destination.Id, movement.DestinationOrganizationId);
        Assert.Equal(destination.Name, movement.DestinationOrganizationName);
        Assert.Equal(new DateOnly(2026, 8, 1), movement.EffectiveDate);
    }
}
