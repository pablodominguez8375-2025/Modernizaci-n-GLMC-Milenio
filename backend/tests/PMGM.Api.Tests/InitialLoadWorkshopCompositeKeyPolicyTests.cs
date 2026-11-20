using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadWorkshopCompositeKeyPolicyTests
{
    private static InitialLoadWorkshopMatchPolicy.Incoming In(
        string code = "CSV1", string number = "23", string oriente = "SCL") =>
        new(code, number, "Taller", oriente, "Santiago", "CL");

    private static InitialLoadWorkshopMatchPolicy.Existing Db(
        string code = "LEGACY", string number = "23", string oriente = "SCL") =>
        new(code, number, "Taller", oriente, "Santiago", "CL");

    [Fact]
    public void Csv_local_code_need_not_match_persisted_code()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Exact,
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(In(), [Db()]));
    }

    [Fact]
    public void Same_number_different_oriente_is_distinct()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Absent,
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(In(), [Db(oriente: "VAL")]));
    }

    [Fact]
    public void Duplicate_persisted_composite_keys_fail_closed_even_with_different_codes()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Ambiguous,
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(In(),
                [Db("A"), Db("B")]));
    }

    [Fact]
    public void Empty_oriente_on_either_side_fails_closed()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Unresolved,
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(In(oriente: ""), [Db()]));
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Unresolved,
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(In(), [Db(oriente: "")]));
    }

    [Fact]
    public void Conflicting_persisted_attributes_block_exact_skip()
    {
        var row = Db() with { Name = "Otro Taller" };
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Contradictory,
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(In(), [row]));
    }

    [Fact]
    public void Missing_snapshot_never_authorizes_new_record()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Unresolved,
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(In(), null));
    }
}
