using PMGM.Api.Modules.CandidateIntake;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class CandidateOriginPolicyTests
{
    private static readonly DateOnly Day = new(2026, 10, 21);
    private static readonly Guid Recommender = Guid.NewGuid();

    private static CandidateOriginVerifiedMember Active() =>
        new(Recommender, Day, true, "MEMBRESIA-REAL-VERIFICADA");

    [Fact]
    public void Brother_requires_active_member_evidence_from_server()
    {
        var valid = CandidateOriginPolicy.ValidateNew(
            "brother", Recommender, null, Active(), Day);
        Assert.True(valid.IsValid);
        Assert.Equal(Recommender, valid.RecommenderMemberId);
        Assert.Null(valid.OtherDescription);
        Assert.Equal("ACTIVE_RECOMMENDER_NOT_VERIFIED",
            CandidateOriginPolicy.ValidateNew("brother", Recommender, null, null, Day).ErrorCode);
        Assert.False(CandidateOriginPolicy.ValidateNew("brother", Recommender, null,
            Active() with { IsActive = false }, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("brother", Recommender, null,
            Active() with { AsOf = Day.AddDays(-1) }, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("brother", Recommender, null,
            Active() with { MemberId = Guid.NewGuid() }, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("brother", Recommender, null,
            Active() with { SourceReference = " " }, Day).IsValid);
    }

    [Fact]
    public void Invalid_or_legacy_kind_is_not_accepted_for_creation()
    {
        Assert.Equal("CANDIDATE_ORIGIN_REQUIRED",
            CandidateOriginPolicy.ValidateNew("unregistered", null, null, null, Day).ErrorCode);
        Assert.False(CandidateOriginPolicy.ValidateNew("", null, null, null, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("brother", Guid.Empty, null, null, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("brother", null, null, null, Day).IsValid);
    }

    [Fact]
    public void Internet_and_other_reject_conflicting_fields()
    {
        Assert.True(CandidateOriginPolicy.ValidateNew("internet", null, null, null, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("internet", Recommender, null, Active(), Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("internet", null, "otro", null, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("other", Recommender, "evento", Active(), Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("brother", Recommender, "evento", Active(), Day).IsValid);
    }

    [Fact]
    public void Social_media_is_distinct_from_web_and_cannot_claim_recommender()
    {
        var valid = CandidateOriginPolicy.ValidateNew(
            CandidateOriginPolicy.SocialMedia, null, null, null, Day);
        Assert.True(valid.IsValid);
        Assert.Equal("social_media", valid.Kind);
        Assert.Null(valid.RecommenderMemberId);
        Assert.Null(valid.OtherDescription);
        Assert.Equal("CANDIDATE_ORIGIN_FIELDS_CONFLICT",
            CandidateOriginPolicy.ValidateNew("social_media", Recommender, null, Active(), Day).ErrorCode);
        Assert.Equal("CANDIDATE_ORIGIN_FIELDS_CONFLICT",
            CandidateOriginPolicy.ValidateNew("social_media", null, "Instagram", null, Day).ErrorCode);
        Assert.NotEqual("internet", valid.Kind);
        Assert.True(CandidateOriginPolicy.ValidateNew("other", null, "Reunión cultural", null, Day).IsValid);
    }

    [Fact]
    public void Other_normalizes_bounded_description_and_blocks_control_characters()
    {
        var valid = CandidateOriginPolicy.ValidateNew("other", null, "  Feria pública  ", null, Day);
        Assert.True(valid.IsValid);
        Assert.Equal("Feria pública", valid.OtherDescription);
        Assert.False(CandidateOriginPolicy.ValidateNew("other", null, " ", null, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("other", null, new string('a', 201), null, Day).IsValid);
        Assert.True(CandidateOriginPolicy.ValidateNew("other", null, new string('a', 200), null, Day).IsValid);
        Assert.False(CandidateOriginPolicy.ValidateNew("other", null, "dos\nlíneas", null, Day).IsValid);
    }

    [Fact]
    public void Recommender_must_not_be_appointed_interviewer()
    {
        Assert.False(CandidateOriginPolicy.CanBeInterviewer(Recommender, Recommender));
        Assert.True(CandidateOriginPolicy.CanBeInterviewer(Guid.NewGuid(), Recommender));
        Assert.True(CandidateOriginPolicy.CanBeInterviewer(Guid.NewGuid(), null));
        Assert.False(CandidateOriginPolicy.CanBeInterviewer(Guid.Empty, null));
    }
}
