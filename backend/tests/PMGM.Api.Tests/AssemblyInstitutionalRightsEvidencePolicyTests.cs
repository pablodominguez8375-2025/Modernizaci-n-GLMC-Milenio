using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyInstitutionalRightsEvidencePolicyTests
{
    private static readonly Guid Person = Guid.NewGuid();
    private static readonly Guid Lodge = Guid.NewGuid();
    private static readonly DateOnly Cutoff = new(2026, 10, 19);
    private static AssemblyMembershipRightsEvidence Membership() =>
        new(Person, Lodge, Cutoff, true, true, true, "PADRON-MEMBRESIA");
    private static AssemblyRightsSourceCoverage Coverage(string source) =>
        new(Person, Cutoff, source, true, true, "CUBIERTA-" + source);
    private static AssemblyInstitutionalRightsResult Evaluate(
        AssemblyMembershipRightsEvidence? member = null,
        AssemblyRightsSourceCoverage? honor = null,
        AssemblyRightsSourceCoverage? regimen = null,
        IReadOnlyCollection<AssemblyRightsRestrictionEvidence>? restrictions = null) =>
        AssemblyInstitutionalRightsEvidencePolicy.Evaluate(Person, Lodge, Cutoff,
            member ?? Membership(), honor ?? Coverage("tribunal_honor"),
            regimen ?? Coverage("regimen_interior"), restrictions ?? []);

    [Fact]
    public void Certified_coverage_and_no_restrictions_support_rights()
    {
        var result = Evaluate();
        Assert.True(result.MemberActive);
        Assert.True(result.InstitutionalRightsActive);
        Assert.True(result.HonorAllowsSuffrage);
        Assert.True(result.RegimenAllowsAttendance);
    }

    [Fact]
    public void Missing_or_stale_coverage_never_proves_absence_of_sanctions()
    {
        var result = AssemblyInstitutionalRightsEvidencePolicy.Evaluate(
            Person, Lodge, Cutoff, Membership(), null,
            Coverage("regimen_interior"), []);
        Assert.Null(result.HonorAllowsSuffrage);
        Assert.Null(result.MemberActive);

        var stale = Evaluate(honor: Coverage("tribunal_honor") with
            { Cutoff = Cutoff.AddDays(-1) });
        Assert.Null(stale.HonorAllowsAttendance);
        var unverified = Evaluate(regimen: Coverage("regimen_interior") with
            { Complete = false });
        Assert.Null(unverified.RegimenAllowsSuffrage);
        var foreign = Evaluate(member: Membership() with { WorkshopId = Guid.NewGuid() });
        Assert.Null(foreign.MemberActive);
    }

    [Fact]
    public void Tribunal_and_regimen_restrictions_apply_to_correct_rights()
    {
        var hospital = new AssemblyRightsRestrictionEvidence(Person, "tribunal_honor",
            Cutoff.AddDays(-5), null, null, false, true, false, true, "TH-EXPEDIENTE-REF");
        var restriction = new AssemblyRightsRestrictionEvidence(Person, "regimen_interior",
            Cutoff.AddDays(-5), null, null, true, false, false, true, "RI-RESOLUCION-REF");
        var result = Evaluate(restrictions: [hospital, restriction]);
        Assert.False(result.HonorAllowsSuffrage);
        Assert.True(result.HonorAllowsAttendance);
        Assert.False(result.RegimenAllowsAttendance);
        Assert.True(result.RegimenAllowsSuffrage);
    }

    [Fact]
    public void Expired_or_revoked_before_cutoff_restriction_does_not_apply()
    {
        var revoked = new AssemblyRightsRestrictionEvidence(Person, "tribunal_honor",
            Cutoff.AddDays(-15), null, Cutoff.AddDays(-2),
            true, true, true, true, "TH-RESOLUCION-REVOCACION");
        var expired = revoked with
        {
            EndsOn = Cutoff.AddDays(-1), RevokedOn = null
        };
        Assert.True(Evaluate(restrictions: [revoked, expired]).HonorAllowsSuffrage);
    }

    [Fact]
    public void Foreign_person_or_unverified_restriction_fails_closed()
    {
        var bad = new AssemblyRightsRestrictionEvidence(Guid.NewGuid(), "tribunal_honor",
            Cutoff.AddDays(-1), null, null, false, true, false, true, "TH-REF");
        Assert.Null(Evaluate(restrictions: [bad]).HonorAllowsSuffrage);
        Assert.Null(Evaluate(restrictions: [bad with { PersonId = Person, Verified = false }])
            .RegimenAllowsSuffrage);
    }
}
