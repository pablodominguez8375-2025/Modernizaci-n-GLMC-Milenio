using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyRosterListingBuilderTests
{
    private static readonly DateOnly Cutoff = new(2026, 10, 19);
    private static readonly AssemblyRecord Assembly = new()
    {
        Name = "Asamblea sintética", MeetingDate = new DateOnly(2026, 11, 19),
        CutoffDate = Cutoff, CreatedBySubject = "qa"
    };

    [Theory]
    [InlineData("12345678-5", "12.345.***-5")]
    [InlineData("12.345.678-5", "12.345.***-5")]
    [InlineData("9876543-K", "9.876.***-K")]
    [InlineData("00000000-0", "00.000.***-0")]
    [InlineData("123-4", "***-4")]
    [InlineData("", "***")]
    [InlineData("abc", "***")]
    public void Rut_is_always_masked(string input, string expected) =>
        Assert.Equal(expected, AssemblyRutMask.Mask(input));

    private static AssemblyMemberStatusRecord Member(Guid org, string last, string first, string rut, string workshop) => new()
    {
        AssemblyId = Assembly.Id, PersonId = Guid.NewGuid(), MemberId = Guid.NewGuid(),
        OrganizationId = org, Category = AssemblyMemberCategoryPolicy.Representative,
        SourceOfficeAssignmentId = Guid.NewGuid(), SourceEvidenceReference = "VM-SINTETICO",
        LastNamesAtCutoff = last, FirstNamesAtCutoff = first, RutAtCutoff = rut,
        WorkshopAtCutoff = workshop
    };

    private static AssemblyEligibilityRecord Eligible(AssemblyMemberStatusRecord m) => new()
    {
        AssemblyId = Assembly.Id, MemberStatusId = m.Id,
        Status = AssemblyVoterEligibilityPolicy.Eligible, MayAttend = true, MayVote = true,
        ReasonCode = "HABILITACION_VERIFICADA", GrandTreasuryPendingAmount = 0m,
        GrandTreasuryCurrency = "CLP", GrandTreasurySourceReference = "GT-1",
        GrandTreasuryAsOfDate = Cutoff, InstitutionalRightsSourceReference = "RI-1",
        HonorRightsSourceReference = "TH-1", EvaluatedBySubject = "qa"
    };

    private static AssemblyEligibilityRecord Debt(AssemblyMemberStatusRecord m) => new()
    {
        AssemblyId = Assembly.Id, MemberStatusId = m.Id,
        Status = AssemblyVoterEligibilityPolicy.AttendanceOnly, MayAttend = true, MayVote = false,
        ReasonCode = AssemblyRosterListingBuilder.DebtReasonCode, EvaluatedBySubject = "qa"
    };

    private static AssemblyDebtDecisionEventRecord Event(Guid org, string action, Guid? reverses = null) => new()
    {
        AssemblyId = Assembly.Id, OrganizationId = org, Action = action,
        Authority = AssemblyVoterEligibilityPolicy.RegimenInteriorAuthority,
        DecidedBySubject = "ri", Reason = "Acuerdo de pago acreditado", EvidenceReference = "ACTA-1",
        ReversesEventId = reverses
    };

    [Fact]
    public void Listings_are_alphabetical_by_surname_and_never_expose_full_rut()
    {
        var org = Guid.NewGuid();
        var zeta = Member(org, "Zúñiga Pérez", "Ana", "11111111-1", "Taller A");
        var alvarez = Member(org, "Álvarez Soto", "Luis", "22222222-2", "Taller A");
        var pending = Member(org, "Mora", "Pedro", "33333333-3", "Taller A");
        var result = AssemblyRosterListingBuilder.Build(Assembly, [zeta, alvarez, pending],
            [Eligible(zeta), Eligible(alvarez)], []);

        Assert.Equal(["Álvarez Soto, Luis", "Mora, Pedro", "Zúñiga Pérez, Ana"],
            result.Asambleistas.Select(r => r.Name).ToArray());
        Assert.Equal(["Álvarez Soto, Luis", "Zúñiga Pérez, Ana"],
            result.Sufragantes.Select(r => r.Name).ToArray());
        Assert.All(result.Asambleistas, r => Assert.DoesNotContain("11111111", r.Rut));
        Assert.All(result.Sufragantes, r => Assert.Equal("", r.Signature));
        Assert.Equal("pendiente", result.Asambleistas.Single(r => r.Name.StartsWith("Mora")).Status);
        Assert.True(result.Provisional);
        Assert.False(result.OfficiallyClosed);
        Assert.False(result.MayIssueBallots);
    }

    [Fact]
    public void Any_pending_balance_blocks_suffrage_and_only_an_active_ri_lift_releases_it()
    {
        var org = Guid.NewGuid();
        var other = Guid.NewGuid();
        var a = Member(org, "Araya", "Beto", "44444444-4", "Taller Moroso");
        var b = Member(other, "Bravo", "Carla", "55555555-5", "Taller Al Día");

        var blocked = AssemblyRosterListingBuilder.Build(Assembly, [a, b], [Debt(a), Eligible(b)], []);
        Assert.DoesNotContain(blocked.Sufragantes, r => r.Name.StartsWith("Araya"));
        var row = blocked.Asambleistas.Single(r => r.Name.StartsWith("Araya"));
        Assert.True(row.BlockedByDebt);
        Assert.False(row.MayVote);
        Assert.Equal("Taller Moroso", blocked.TalleresConBloqueo.Single().Workshop);

        var lift = Event(org, "lift");
        var lifted = AssemblyRosterListingBuilder.Build(Assembly, [a, b], [Debt(a), Eligible(b)], [lift]);
        Assert.Contains(lifted.Sufragantes, r => r.Name.StartsWith("Araya"));
        Assert.True(lifted.Asambleistas.Single(r => r.Name.StartsWith("Araya")).BlockLifted);

        var revoked = AssemblyRosterListingBuilder.Build(Assembly, [a, b], [Debt(a), Eligible(b)],
            [lift, Event(org, "revoke", lift.Id)]);
        Assert.DoesNotContain(revoked.Sufragantes, r => r.Name.StartsWith("Araya"));
    }

    [Fact]
    public void A_lift_for_another_workshop_does_not_release_this_workshop()
    {
        var org = Guid.NewGuid();
        var a = Member(org, "Araya", "Beto", "44444444-4", "Taller Moroso");
        var result = AssemblyRosterListingBuilder.Build(Assembly, [a], [Debt(a)],
            [Event(Guid.NewGuid(), "lift")]);
        Assert.Empty(result.Sufragantes);
    }
}
