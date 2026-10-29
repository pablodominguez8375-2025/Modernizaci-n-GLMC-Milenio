using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.SecretariatOperations;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class MemberHistoryValidationTests
{
    private static MemberHistoryItemRequest Degree(string action, string type, DateOnly date, Guid? target = null)
        => new("degree", action, target, null, null, null, null, type, date);

    private static MemberHistoryItemRequest Office(string action, string type, string period, Guid? target = null)
        => new("office", action, target, type, period, new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31), null, null);

    private static MemberHistoryProposalRequest Request(params MemberHistoryItemRequest[] items)
        => new("Acta N° 12 del Taller", "Se completa la historia del hermano al migrar el Cuadro.", items);

    private static DegreeEvent Event(string type, DateOnly date) => new()
    {
        MemberId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Degree = "apprentice", EventType = type, EffectiveDate = date,
    };

    [Fact]
    public void Accepts_a_new_office_and_consistent_milestones()
    {
        var error = MemberHistoryEndpoints.Validate(
            Request(Office("add", "orator", "2019"), Degree("add", MembershipCodes.DegreeEvent.Initiation, new DateOnly(2010, 5, 1))), [], []);
        Assert.Null(error);
    }

    [Fact]
    public void Rejects_missing_evidence_short_reason_and_empty_changes()
    {
        Assert.NotNull(MemberHistoryEndpoints.Validate(new("", "Motivo suficientemente largo", [Office("add", "orator", "2019")]), [], []));
        Assert.NotNull(MemberHistoryEndpoints.Validate(new("Acta", "corto", [Office("add", "orator", "2019")]), [], []));
        Assert.NotNull(MemberHistoryEndpoints.Validate(Request(), [], []));
    }

    [Fact]
    public void Rejects_unknown_office_and_inverted_period()
    {
        Assert.NotNull(MemberHistoryEndpoints.Validate(Request(Office("add", "inventado", "2019")), [], []));
        var inverted = new MemberHistoryItemRequest("office", "add", null, "orator", "2019", new DateOnly(2019, 12, 31), new DateOnly(2019, 1, 1), null, null);
        Assert.NotNull(MemberHistoryEndpoints.Validate(Request(inverted), [], []));
    }

    [Fact]
    public void Adding_an_existing_milestone_requires_correction_instead()
    {
        var existing = Event(MembershipCodes.DegreeEvent.Initiation, new DateOnly(2010, 5, 1));
        var error = MemberHistoryEndpoints.Validate(Request(Degree("add", MembershipCodes.DegreeEvent.Initiation, new DateOnly(2011, 1, 1))), [], [existing]);
        Assert.Contains("Corregir", error);
        Assert.Null(MemberHistoryEndpoints.Validate(Request(Degree("replace", MembershipCodes.DegreeEvent.Initiation, new DateOnly(2011, 1, 1), existing.Id)), [], [existing]));
    }

    [Fact]
    public void Rejects_a_correction_that_breaks_chronology()
    {
        var initiation = Event(MembershipCodes.DegreeEvent.Initiation, new DateOnly(2010, 5, 1));
        var wage = Event(MembershipCodes.DegreeEvent.WageIncrease, new DateOnly(2012, 5, 1));
        var error = MemberHistoryEndpoints.Validate(
            Request(Degree("replace", MembershipCodes.DegreeEvent.Initiation, new DateOnly(2013, 1, 1), initiation.Id)), [], [initiation, wage]);
        Assert.Contains("cronológicamente", error);
    }

    [Fact]
    public void Rejects_future_milestones_and_duplicate_changes()
    {
        Assert.NotNull(MemberHistoryEndpoints.Validate(
            Request(Degree("add", MembershipCodes.DegreeEvent.Exaltation, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30))), [], []));
        Assert.NotNull(MemberHistoryEndpoints.Validate(
            Request(Office("add", "orator", "2019"), Office("add", "orator", "2019")), [], []));
    }

    [Fact]
    public void Office_correction_must_target_an_existing_office()
    {
        var office = new OfficeAssignment { MemberId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), OfficeType = "orator", Period = "2019" };
        Assert.Null(MemberHistoryEndpoints.Validate(Request(Office("replace", "secretary", "2019", office.Id)), [office], []));
        Assert.NotNull(MemberHistoryEndpoints.Validate(Request(Office("replace", "secretary", "2019", Guid.NewGuid())), [office], []));
        Assert.NotNull(MemberHistoryEndpoints.Validate(Request(Office("add", "orator", "2019")), [office], []));
    }
}
