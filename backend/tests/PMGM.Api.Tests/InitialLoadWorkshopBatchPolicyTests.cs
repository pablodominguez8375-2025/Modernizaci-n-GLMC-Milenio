using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadWorkshopBatchPolicyTests
{
    private static InitialLoadWorkshopMatchPolicy.Incoming In(string code, string name = "Workshop") =>
        new(code, "23", name, "SCL", "Santiago", "CL");
    private static InitialLoadWorkshopMatchPolicy.Existing Db(string code, string name = "Workshop") =>
        new(code, "23", name, "SCL", "Santiago", "CL");

    [Fact]
    public void Mixed_exact_and_new_rows_are_counted_without_authorizing_writes()
    {
        var result = InitialLoadWorkshopBatchPolicy.Inspect([In("A"), In("B")], [Db("A")]);
        Assert.True(result.Valid);
        Assert.Equal(1, result.NewRows);
        Assert.Equal(1, result.ExactSkips);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Conflicting_existing_record_blocks_entire_batch_without_partial_counts()
    {
        var result = InitialLoadWorkshopBatchPolicy.Inspect(
            [In("A", "Different"), In("B")], [Db("A")]);
        Assert.False(result.Valid);
        Assert.Equal("BATCH_NATURAL_KEY_CONFLICT", result.Code);
        Assert.Equal(0, result.NewRows);
        Assert.Equal(0, result.ExactSkips);
        Assert.Equal(1, result.BlockedRows);
    }

    [Fact]
    public void Duplicate_incoming_or_persisted_keys_fail_closed()
    {
        Assert.Equal("BATCH_KEYS_INVALID_OR_DUPLICATE",
            InitialLoadWorkshopBatchPolicy.Inspect([In("A"), In("A")], []).Code);
        Assert.Equal("SNAPSHOT_KEYS_AMBIGUOUS",
            InitialLoadWorkshopBatchPolicy.Inspect([In("A")], [Db("A"), Db("A")]).Code);
        Assert.Equal("SNAPSHOT_KEYS_AMBIGUOUS",
            InitialLoadWorkshopBatchPolicy.Inspect([In("A")], [Db("")]).Code);
        Assert.Equal("SNAPSHOT_UNAVAILABLE",
            InitialLoadWorkshopBatchPolicy.Inspect(null, []).Code);
    }
}
