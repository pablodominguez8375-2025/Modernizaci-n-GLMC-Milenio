using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadWorkshopMatchPolicyTests
{
    private static InitialLoadWorkshopMatchPolicy.Incoming Input(string code = "T23") =>
        new(code, "23", "Workshop", "SCL", "Santiago", "CL");

    private static InitialLoadWorkshopMatchPolicy.Existing Existing(string code = "T23",
        string name = "Workshop") =>
        new(code, "23", name, "SCL", "Santiago", "CL");

    [Fact]
    public void Exact_matching_uses_explicit_code_and_all_canonical_fields()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Exact,
            InitialLoadWorkshopMatchPolicy.Inspect(Input(), [Existing()]));
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Contradictory,
            InitialLoadWorkshopMatchPolicy.Inspect(Input(), [Existing(name: "Other")]));
    }

    [Fact]
    public void Missing_key_and_ambiguous_snapshot_fail_closed()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Unresolved,
            InitialLoadWorkshopMatchPolicy.Inspect(Input(""), [Existing()]));
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Unresolved,
            InitialLoadWorkshopMatchPolicy.Inspect(Input(), null));
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Ambiguous,
            InitialLoadWorkshopMatchPolicy.Inspect(Input(), [Existing(), Existing()]));
    }

    [Fact]
    public void Incomplete_canonical_snapshot_cannot_be_treated_as_absent()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Unresolved,
            InitialLoadWorkshopMatchPolicy.Inspect(Input("NEW"), [Existing("")]));
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Unresolved,
            InitialLoadWorkshopMatchPolicy.Inspect(Input(), [Existing(), Existing("")]));
    }

    [Fact]
    public void Workshop_number_is_never_used_as_workshop_code()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Absent,
            InitialLoadWorkshopMatchPolicy.Inspect(Input("OTHER"), [Existing()]));
    }
}
