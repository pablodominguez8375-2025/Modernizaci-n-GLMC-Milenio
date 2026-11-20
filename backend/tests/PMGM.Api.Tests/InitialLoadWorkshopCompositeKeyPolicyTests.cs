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
    public void Composite_batch_skips_existing_even_if_csv_local_code_differs()
    {
        var result = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
            [In("CSV1"), In("CSV2", "24", "SCL")], [Db("LEGACY")]);
        Assert.True(result.Valid);
        Assert.Equal(1, result.ExactSkips);
        Assert.Equal(1, result.NewRows);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Composite_batch_rejects_duplicate_database_identity_under_different_codes()
    {
        var result = InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
            [In()], [Db("OLD1"), Db("OLD2")]);
        Assert.False(result.Valid);
        Assert.Equal("SNAPSHOT_KEYS_AMBIGUOUS", result.Code);
        Assert.Equal(0, result.NewRows);
    }

    [Fact]
    public void Composite_batch_rejects_duplicate_incoming_identity_and_empty_batch()
    {
        Assert.Equal("BATCH_KEYS_INVALID_OR_DUPLICATE",
            InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity(
                [In("A"), In("B")], []).Code);
        Assert.Equal("BATCH_EMPTY",
            InitialLoadWorkshopBatchPolicy.InspectCompositeIdentity([], []).Code);
    }

    [Fact]
    public void Missing_snapshot_never_authorizes_new_record()
    {
        Assert.Equal(InitialLoadExistingRecordPolicy.Match.Unresolved,
            InitialLoadWorkshopCompositeKeyPolicy.Inspect(In(), null));
    }
}
