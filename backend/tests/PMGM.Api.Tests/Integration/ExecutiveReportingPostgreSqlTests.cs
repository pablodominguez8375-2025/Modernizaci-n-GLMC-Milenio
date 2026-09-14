using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.ExecutiveReporting;
using PMGM.Api.Modules.Hospitalaria.Entities;
using PMGM.Api.Modules.LodgeManagement.Entities;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.Treasury.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class ExecutiveReportingPostgreSqlTests
{
    [Fact]
    public async Task Report_preserves_degree_and_past_active_after_workshop_transfer()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        var mainOptions = new DbContextOptionsBuilder<PmgmDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var lodgeOptions = new DbContextOptionsBuilder<LodgeManagementDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new PmgmDbContext(mainOptions);
        await using var lodgeDb = new LodgeManagementDbContext(lodgeOptions);
        await db.Database.MigrateAsync(cancellationToken);
        await lodgeDb.Database.MigrateAsync(cancellationToken);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var source = new Organization { Name = $"Taller Origen {suffix}", Number = $"9{suffix[..2]}", Type = "workshop" };
        var target = new Organization { Name = $"Taller Destino {suffix}", Number = $"8{suffix[..2]}", Type = "workshop" };
        var person = new Person { FirstNames = "Hermano", LastNames = $"Trasladado {suffix}" };
        var member = new Member { Person = person, InstitutionalNumber = $"QA-{suffix}" };
        var sourceMembership = new Membership
        {
            Member = member,
            Organization = source,
            MembershipType = "regular",
            StartDate = new DateOnly(2018, 1, 1),
            EndDate = new DateOnly(2025, 12, 31),
            Status = MembershipCodes.MembershipStatus.Transferred,
            EndReason = "Traslado QA"
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
        db.DegreeEvents.Add(new DegreeEvent
        {
            Member = member,
            Organization = source,
            Degree = "master",
            EventType = MembershipCodes.DegreeEvent.Exaltation,
            EffectiveDate = new DateOnly(2022, 6, 15)
        });
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
            ExecutedAtUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
        });
        db.FinancialRegularitySnapshots.AddRange(
            new FinancialRegularitySnapshot
            {
                Organization = target,
                Member = member,
                Scope = "member",
                Status = "delinquent",
                AsOfDate = new DateOnly(2026, 9, 1)
            },
            new FinancialRegularitySnapshot
            {
                Organization = target,
                MemberId = null,
                Scope = "organization",
                Status = "delinquent",
                AsOfDate = new DateOnly(2026, 9, 1)
            });
        db.HospitalariaRegularitySnapshots.Add(new HospitalariaRegularitySnapshot
        {
            Organization = target,
            Status = "overdue",
            AsOfDate = new DateOnly(2026, 9, 1)
        });
        await db.SaveChangesAsync(cancellationToken);

        lodgeDb.LodgeMeetings.Add(new LodgeMeeting
        {
            OrganizationId = target.Id,
            MeetingDate = new DateOnly(2026, 8, 20),
            MeetingType = "regular",
            Grade = "master",
            Status = "closed"
        });
        lodgeDb.LodgeInstructionSessions.Add(new LodgeInstructionSession
        {
            OrganizationId = target.Id,
            InstructionDate = new DateOnly(2026, 8, 22),
            Grade = "master",
            Topic = "Docencia QA",
            ResponsibleOffice = "first_warden",
            Status = "closed",
            CreatedBySubject = "qa"
        });
        await lodgeDb.SaveChangesAsync(cancellationToken);

        var service = new ExecutiveReportingService(db, lodgeDb);
        var report = await service.BuildAsync(
            new DateOnly(2026, 9, 9),
            new DateOnly(2026, 1, 1),
            cancellationToken);

        var targetRow = Assert.Single(report.Workshops, x => x.OrganizationId == target.Id);
        Assert.Equal(1, targetRow.CurrentMembers);
        Assert.Equal(1, targetRow.ActiveMembers);
        Assert.Equal(1, targetRow.PastActive);
        Assert.Equal(1, targetRow.DegreeDistribution["master"]);
        Assert.Equal(1, targetRow.Financial.DelinquentAffiliations);
        Assert.Equal("delinquent", targetRow.Financial.WorkshopTreasuryStatus);
        Assert.Equal("overdue", targetRow.Financial.WorkshopHospitalariaStatus);
        Assert.Equal(1, targetRow.Activity.Meetings);
        Assert.Equal(1, targetRow.Activity.InstructionSessions);
        Assert.True(targetRow.AttentionRequired);
        Assert.Contains("treasury_delinquent", targetRow.AttentionFlags);
        Assert.Contains("hospitalaria_overdue", targetRow.AttentionFlags);
    }
}
