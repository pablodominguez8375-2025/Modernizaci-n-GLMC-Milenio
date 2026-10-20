using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyPreliminaryReviewProjectionTests
{
    private static readonly DateOnly Cutoff = new(2026, 10, 19);

    private static ClaimsPrincipal Actor(string role, string scope = "order", bool authenticated = true) =>
        new(new ClaimsIdentity([
            new Claim("sub", "sujeto-sintetico"),
            new Claim(InstitutionalClaims.Role, role),
            new Claim(InstitutionalClaims.Scope, scope)
        ], authenticated ? "Synthetic" : null));

    [Theory]
    [InlineData(InstitutionalRoles.RegimenInterior)]
    [InlineData(InstitutionalRoles.GranSecretaria)]
    public void Authorised_order_officers_can_read_preliminary_review(string role) =>
        Assert.True(AssemblyPreliminaryReviewEndpoints.CanRead(Actor(role)));

    [Theory]
    [InlineData(InstitutionalRoles.PlatformSuperAdmin)]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    [InlineData(InstitutionalRoles.GranTesoreria)]
    [InlineData(InstitutionalRoles.TallerTesoreria)]
    public void Other_roles_are_not_silently_promoted_to_internal_affairs(string role) =>
        Assert.False(AssemblyPreliminaryReviewEndpoints.CanRead(Actor(role)));

    [Fact]
    public void Lodge_scope_and_unauthenticated_identity_do_not_grant_access()
    {
        Assert.False(AssemblyPreliminaryReviewEndpoints.CanRead(
            Actor(InstitutionalRoles.RegimenInterior, scope: "lodge")));
        Assert.False(AssemblyPreliminaryReviewEndpoints.CanRead(
            Actor(InstitutionalRoles.RegimenInterior, authenticated: false)));
    }

    private static AssemblyMemberStatusRecord Member() => new()
    {
        AssemblyId = Guid.NewGuid(),
        PersonId = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        MemberId = Guid.NewGuid(),
        Category = AssemblyMemberCategoryPolicy.Representative,
        SourceOfficeAssignmentId = Guid.NewGuid(),
        SourceEvidenceReference = "VM-SINTETICO",
        LastNamesAtCutoff = "Apellido",
        FirstNamesAtCutoff = "Nombre",
        RutAtCutoff = "00000000-0",
        WorkshopAtCutoff = "Taller ficticio"
    };

    private static AssemblyEligibilityRecord Decision(
        AssemblyMemberStatusRecord member, Guid? supersedes = null,
        string status = AssemblyVoterEligibilityPolicy.Eligible) => new()
    {
        AssemblyId = member.AssemblyId,
        MemberStatusId = member.Id,
        Status = status,
        MayAttend = true, MayVote = status == AssemblyVoterEligibilityPolicy.Eligible,
        ReasonCode = "HABILITACION_VERIFICADA",
        GrandTreasurySourceReference = "GT-CORTE",
        GrandTreasuryCurrency = "CLP",
        GrandTreasuryAsOfDate = Cutoff,
        InstitutionalRightsSourceReference = "RI-RESOLUCION",
        HonorRightsSourceReference = "TH-COBERTURA",
        EvaluatedBySubject = "funcionario-ri",
        SupersedesEligibilityId = supersedes
    };

    [Fact]
    public void Unique_tip_of_append_only_history_is_selected_but_is_not_final_vote()
    {
        var m = Member();
        var previous = Decision(m);
        previous.Status = AssemblyVoterEligibilityPolicy.AttendanceOnly;
        previous.MayVote = false;
        var last = Decision(m, supersedes: previous.Id);
        var projected = AssemblyPreliminaryReviewProjection.Project(m, [previous, last], Cutoff);
        Assert.Equal(AssemblyVoterEligibilityPolicy.Eligible, projected.Status);
        Assert.True(projected.MayVotePreliminary);
        Assert.False(projected.MayVoteConfirmed);
        Assert.Equal(m.PersonId, projected.PersonId);
        Assert.Equal(m.OrganizationId, projected.OrganizationId);
    }

    [Fact]
    public void Unknown_or_forked_review_history_fails_closed()
    {
        var m = Member();
        Assert.Equal(AssemblyVoterEligibilityPolicy.Pending,
            AssemblyPreliminaryReviewProjection.Project(m, [], Cutoff).Status);

        var first = Decision(m);
        var second = Decision(m);
        var result = AssemblyPreliminaryReviewProjection.Project(m, [first, second], Cutoff);
        Assert.Equal("CONFLICTING_EVALUATION_HISTORY", result.ReasonCode);
        Assert.False(result.MayVoteConfirmed);
    }

    [Fact]
    public void Invalid_financial_source_cannot_appear_preliminarily_eligible()
    {
        var m = Member();
        var d = Decision(m);
        d.GrandTreasurySourceReference = null;
        var result = AssemblyPreliminaryReviewProjection.Project(m, [d], Cutoff);
        Assert.Equal("ELIGIBILITY_SOURCES_UNVERIFIED", result.ReasonCode);
        Assert.False(result.MayVotePreliminary);

        d.GrandTreasurySourceReference = "GT-CORTE";
        d.GrandTreasuryAsOfDate = Cutoff.AddDays(-1);
        Assert.False(AssemblyPreliminaryReviewProjection.Project(m, [d], Cutoff).MayVotePreliminary);
    }

    [Fact]
    public void Foreign_history_does_not_join_another_assembly_member()
    {
        var m = Member();
        var other = Decision(m);
        other.AssemblyId = Guid.NewGuid();
        var projected = AssemblyPreliminaryReviewProjection.Project(m, [other], Cutoff);
        Assert.Equal(AssemblyVoterEligibilityPolicy.Pending, projected.Status);
        Assert.False(projected.MayVoteConfirmed);
    }
}
