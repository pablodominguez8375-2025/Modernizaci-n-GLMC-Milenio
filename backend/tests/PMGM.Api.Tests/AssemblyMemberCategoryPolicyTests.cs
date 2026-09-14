using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyMemberCategoryPolicyTests
{
    private static readonly DateOnly Cutoff = new(2026, 9, 12);

    [Fact]
    public void First_current_term_is_representative()
    {
        var result = AssemblyMemberCategoryPolicy.Classify(
            [new(new DateOnly(2026, 1, 1), null)], Cutoff);

        Assert.Equal(AssemblyMemberCategoryPolicy.Representative, result.Category);
        Assert.Equal(0, result.CompletedTerms);
        Assert.True(result.CurrentlyServing);
    }

    [Fact]
    public void Completed_term_grants_permanent_category()
    {
        var result = AssemblyMemberCategoryPolicy.Classify(
            [new(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31))], Cutoff);

        Assert.Equal(AssemblyMemberCategoryPolicy.PermanentAssemblyMember, result.Category);
        Assert.True(result.Qualifies);
    }

    [Fact]
    public void Reelected_master_remains_permanent_assembly_member()
    {
        var result = AssemblyMemberCategoryPolicy.Classify(
            [
                new(new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31)),
                new(new DateOnly(2026, 1, 1), null)
            ], Cutoff);

        Assert.Equal(AssemblyMemberCategoryPolicy.PermanentAssemblyMember, result.Category);
        Assert.Equal(1, result.CompletedTerms);
        Assert.True(result.CurrentlyServing);
    }

    [Fact]
    public void Future_or_incomplete_historical_assignment_does_not_grant_category()
    {
        var result = AssemblyMemberCategoryPolicy.Classify(
            [new(new DateOnly(2027, 1, 1), null)], Cutoff);

        Assert.False(result.Qualifies);
        Assert.Null(result.Category);
    }
}
