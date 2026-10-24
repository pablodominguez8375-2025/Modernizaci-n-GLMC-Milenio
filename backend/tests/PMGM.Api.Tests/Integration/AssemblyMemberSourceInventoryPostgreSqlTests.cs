using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class AssemblyMemberSourceInventoryPostgreSqlTests
{
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static PmgmDbContext Open(string cs) => new(
        new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(cs).Options);

    [Fact]
    public async Task Source_inventory_reads_real_member_mappings_but_never_certifies_Vm_or_votes()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var db = Open(cs);
        await db.Database.MigrateAsync(ct);

        var org = new Organization { Name = "Taller real ficticio", Type = "workshop" };
        var historical = new Organization { Name = "Taller histórico ficticio", Type = "workshop" };
        var person = new Person { FirstNames = "Ana", LastNames = "Álvarez", Rut = "12.345.678-5" };
        var member = new Member { Person = person, PersonId = person.Id };
        var code = "vm-c24-" + Guid.NewGuid().ToString("N");
        var enrollment = new Membership
        {
            MemberId = member.Id, OrganizationId = org.Id, Status = "active",
            MembershipType = "regular", StartDate = new DateOnly(2025, 1, 1)
        };
        var office = new OfficeAssignment
        {
            MemberId = member.Id, OrganizationId = historical.Id,
            OfficeType = code, Period = "2020-2021 (dato no acreditado)",
            StartDate = new DateOnly(2020, 1, 1),
            EndDate = new DateOnly(2021, 12, 31), EvidenceReference = "ACTA-PENDIENTE"
        };
        db.AddRange(org, historical, person, member, enrollment, office);
        await db.SaveChangesAsync(ct);

        var result = await AssemblyMemberSourceInventory.DiscoverAsync(db, Cutoff, code, ct);
        Assert.True(result.InventoryReadCompleted);
        Assert.False(result.InstitutionallyCertified);
        Assert.Empty(result.BlockingReasons);
        var fact = Assert.Single(result.CandidateFacts);
        Assert.Equal(person.Id, fact.PersonId);
        Assert.Equal(org.Id, fact.MembershipWorkshopId);
        Assert.Equal(historical.Id, Assert.Single(fact.MasterOffices).HistoricalWorkshopId);
        Assert.Equal(office.Id, fact.MasterOffices[0].OfficeAssignmentId);
        Assert.Equal("Álvarez", fact.LastNames);
        Assert.Equal("12.345.678-5", fact.Rut);
    }

    [Fact]
    public async Task Duplicate_workshop_memberships_block_the_entire_source_inventory()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var db = Open(cs);
        await db.Database.MigrateAsync(ct);

        var orgA = new Organization { Name = "Taller A sintético", Type = "workshop" };
        var orgB = new Organization { Name = "Taller B sintético", Type = "workshop" };
        var person = new Person { FirstNames = "Beatriz", LastNames = "Prueba" };
        var member = new Member { Person = person, PersonId = person.Id };
        var code = "vm-c24-" + Guid.NewGuid().ToString("N");
        var office = new OfficeAssignment
        {
            MemberId = member.Id, OrganizationId = orgA.Id, OfficeType = code,
            Period = "Período ficticio", StartDate = new DateOnly(2025, 1, 1),
            EvidenceReference = "CERTIFICACION-PENDIENTE"
        };
        db.AddRange(orgA, orgB, person, member, office,
            new Membership { MemberId = member.Id, OrganizationId = orgA.Id,
                Status = "active", MembershipType = "regular",
                StartDate = new DateOnly(2025, 1, 1) },
            new Membership { MemberId = member.Id, OrganizationId = orgB.Id,
                Status = "active", MembershipType = "regular",
                StartDate = new DateOnly(2025, 2, 1) });
        await db.SaveChangesAsync(ct);

        var result = await AssemblyMemberSourceInventory.DiscoverAsync(db, Cutoff, code, ct);
        Assert.False(result.InventoryReadCompleted);
        Assert.False(result.InstitutionallyCertified);
        Assert.Empty(result.CandidateFacts);
        Assert.Contains("MEMBERSHIP_OR_WORKSHOP_HISTORY_AMBIGUOUS", result.BlockingReasons);
    }

    [Fact]
    public async Task Support_membership_and_unknown_office_code_never_create_trusted_rows()
    {
        var cs = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var db = Open(cs);
        await db.Database.MigrateAsync(ct);

        var support = new Organization { Name = "Taller 00 QA", Type = "support" };
        var person = new Person { FirstNames = "Carlos", LastNames = "Test" };
        var member = new Member { Person = person, PersonId = person.Id };
        var code = "vm-c24-" + Guid.NewGuid().ToString("N");
        db.AddRange(support, person, member,
            new OfficeAssignment
            {
                MemberId = member.Id, OrganizationId = support.Id,
                OfficeType = code, Period = "Ficticio",
                StartDate = new DateOnly(2026, 1, 1),
                EvidenceReference = "REF-PENDIENTE"
            },
            new Membership
            {
                MemberId = member.Id, OrganizationId = support.Id,
                Status = "active", MembershipType = "regular",
                StartDate = new DateOnly(2026, 1, 1)
            });
        await db.SaveChangesAsync(ct);

        var blocked = await AssemblyMemberSourceInventory.DiscoverAsync(db, Cutoff, code, ct);
        Assert.False(blocked.InventoryReadCompleted);
        Assert.Empty(blocked.CandidateFacts);

        var absent = await AssemblyMemberSourceInventory.DiscoverAsync(
            db, Cutoff, code + "-absent", ct);
        Assert.False(absent.InventoryReadCompleted);
        Assert.Contains("MASTER_OFFICE_RECORDS_MISSING", absent.BlockingReasons);
    }
}
