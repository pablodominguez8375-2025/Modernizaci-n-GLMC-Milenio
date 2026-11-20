using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadSnapshotPlanPolicyTests
{
    [Fact]
    public void Counts_exact_skips_and_new_records_without_writes()
    {
        var result = InitialLoadSnapshotPlanPolicy.Inspect(
            [InitialLoadExistingRecordPolicy.Match.Exact,
             InitialLoadExistingRecordPolicy.Match.Absent,
             InitialLoadExistingRecordPolicy.Match.Exact], 3);
        Assert.True(result.Valid);
        Assert.Equal("SNAPSHOT_PLAN_VALID", result.Code);
        Assert.Equal(3, result.Rows);
        Assert.Equal(1, result.NewRows);
        Assert.Equal(2, result.ExactSkips);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Conflicting_or_unresolved_reference_blocks_entire_plan()
    {
        var result = InitialLoadSnapshotPlanPolicy.Inspect(
            [InitialLoadExistingRecordPolicy.Match.Absent,
             InitialLoadExistingRecordPolicy.Match.Contradictory,
             InitialLoadExistingRecordPolicy.Match.Unresolved,
             InitialLoadExistingRecordPolicy.Match.Ambiguous], 4);
        Assert.False(result.Valid);
        Assert.Equal("SNAPSHOT_PLAN_BLOCKED", result.Code);
        Assert.Equal(1, result.Conflicts);
        Assert.Equal(1, result.Unresolved);
        Assert.Equal(1, result.Ambiguous);
        Assert.False(result.CanApply);
    }

    [Fact]
    public void Missing_or_inconsistent_snapshot_fails_closed()
    {
        Assert.Equal("SNAPSHOT_ROW_COUNT_MISMATCH",
            InitialLoadSnapshotPlanPolicy.Inspect(null, 1).Code);
        Assert.False(InitialLoadSnapshotPlanPolicy.Inspect(
            [InitialLoadExistingRecordPolicy.Match.Exact], 2).Valid);
        Assert.False(InitialLoadSnapshotPlanPolicy.Inspect(
            [(InitialLoadExistingRecordPolicy.Match)99], 1).Valid);
    }
}
