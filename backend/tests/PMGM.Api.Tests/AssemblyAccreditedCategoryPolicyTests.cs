using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyAccreditedCategoryPolicyTests
{
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static AssemblyMasterOfficeEvidence Master(
        int startYear, int? endYear, bool completed, bool current,
        bool verified = true, string? reference = "PL-VMA-ACREDITACION") =>
        new(Guid.NewGuid(), new DateOnly(startYear, 1, 1),
            endYear is null ? null : new DateOnly(endYear.Value, 12, 31),
            verified, reference, completed, current);

    [Fact]
    public void Verified_first_current_term_is_representative()
    {
        var status = AssemblyAccreditedCategoryPolicy.Classify(
            [Master(2026, null, false, true)], Cutoff);
        Assert.Equal(AssemblyMemberCategoryPolicy.Representative, status.Category);
        Assert.True(status.Qualifies);
    }

    [Fact]
    public void A_full_verified_period_makes_a_permanent_member_even_when_reelected()
    {
        var status = AssemblyAccreditedCategoryPolicy.Classify(
            [Master(2024, 2024, true, false), Master(2026, null, false, true)], Cutoff);
        Assert.Equal(AssemblyMemberCategoryPolicy.PermanentAssemblyMember, status.Category);
        Assert.Equal(1, status.CompletedTerms);
        Assert.True(status.CurrentlyServing);
    }

    [Fact]
    public void Mere_past_end_date_never_proves_completed_service()
    {
        var status = AssemblyAccreditedCategoryPolicy.Classify(
            [Master(2024, 2024, false, false)], Cutoff);
        Assert.False(status.Qualifies);
        Assert.Null(status.Category);
    }

    [Fact]
    public void Retired_early_unverified_or_missing_support_is_not_a_permanent_assembly_member()
    {
        var scenarios = new[]
        {
            Master(2024, 2024, true, false, verified: false),
            Master(2024, 2024, true, false, reference: ""),
            Master(2026, 2027, true, false),
            Master(2026, null, false, false),
            Master(2024, 2024, true, false) with { OfficeAssignmentId = Guid.Empty }
        };
        foreach (var scenario in scenarios)
        {
            var status = AssemblyAccreditedCategoryPolicy.Classify([scenario], Cutoff);
            Assert.False(status.Qualifies);
        }
    }

    [Fact]
    public void Two_completed_terms_never_downgrade_permanent_to_representative()
    {
        var result = AssemblyAccreditedCategoryPolicy.Classify(
            [Master(2020, 2020, true, false),
             Master(2023, 2023, true, false)], Cutoff);
        Assert.Equal(2, result.CompletedTerms);
        Assert.Equal(AssemblyMemberCategoryPolicy.PermanentAssemblyMember, result.Category);
    }
}
