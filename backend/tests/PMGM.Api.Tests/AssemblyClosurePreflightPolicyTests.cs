using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyClosurePreflightPolicyTests
{
    private static readonly DateOnly Cutoff = new(2026, 10, 19);
    private static readonly DateTimeOffset CheckedAt =
        new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static AssemblyRecord Assembly() => new()
    {
        Name = "Asamblea sintética de pruebas",
        MeetingDate = Cutoff.AddMonths(1),
        CutoffDate = Cutoff,
        CreatedBySubject = "ri-test"
    };

    private static AssemblyClosureOfficialCoverage Coverage(AssemblyRecord a) =>
        new(a.Id, Cutoff, true, true, true, "RI-PADRON-SINTETICO");

    private static AssemblyClosureCandidateEvidence Candidate(
        AssemblyRecord assembly, string status = AssemblyVoterEligibilityPolicy.Eligible)
    {
        var member = new AssemblyMemberStatusRecord
        {
            AssemblyId = assembly.Id,
            PersonId = Guid.NewGuid(), MemberId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Category = AssemblyMemberCategoryPolicy.PermanentAssemblyMember,
            SourceOfficeAssignmentId = Guid.NewGuid(),
            SourceEvidenceReference = "CARGO-VM-SINTETICO",
            FullTermVerified = true,
            LastNamesAtCutoff = "Prueba",
            FirstNamesAtCutoff = "Sintética",
            RutAtCutoff = "00000000-0",
            WorkshopAtCutoff = "Taller de pruebas",
            VerifiedAtUtc = CheckedAt.AddMinutes(-10)
        };
        var isEligible = status == AssemblyVoterEligibilityPolicy.Eligible;
        var isAttendanceOnly = status == AssemblyVoterEligibilityPolicy.AttendanceOnly;
        var eligibility = new AssemblyEligibilityRecord
        {
            AssemblyId = assembly.Id, MemberStatusId = member.Id,
            Status = status, MayAttend = isEligible || isAttendanceOnly,
            MayVote = isEligible,
            ReasonCode = isEligible ? "HABILITACION_VERIFICADA" : "RESTRICCION_SUFRAGIO",
            GrandTreasuryPendingAmount = 0m,
            GrandTreasuryCurrency = "CLP",
            GrandTreasurySourceReference = "GT-CERT-SINTETICO",
            GrandTreasuryAsOfDate = Cutoff,
            InstitutionalRightsSourceReference = "RI-RES-SINTETICO",
            HonorRightsSourceReference = "TH-ESTADO-SINTETICO",
            EvaluatedBySubject = "ri-evaluador",
            EvaluatedAtUtc = CheckedAt.AddMinutes(-1)
        };
        var sources = new AssemblyClosureMemberSources(
            member.PersonId, member.OrganizationId, Cutoff,
            true, true, true, true, true, true,
            "MEMBRESIA-SINTETICA", "VM-CERT-SINTETICA",
            "GT-CERT-SINTETICO", "CLP",
            "TH-ESTADO-SINTETICO", "RI-RES-SINTETICO", false);
        return new(member, eligibility, sources, CheckedAt);
    }

    private static AssemblyClosurePreflightResult Run(
        AssemblyRecord assembly, params AssemblyClosureCandidateEvidence[] rows) =>
        AssemblyClosurePreflightPolicy.Assess(assembly, rows, Coverage(assembly), Cutoff);

    [Fact]
    public void Verified_preflight_has_stable_digest_but_never_closes_itself()
    {
        var a = Assembly();
        var one = Candidate(a);
        var two = Candidate(a, AssemblyVoterEligibilityPolicy.AttendanceOnly);
        var expected = Run(a, one, two);
        var reordered = Run(a, two, one);
        Assert.True(expected.DocumentaryPreflightReady);
        Assert.False(expected.MayPerformClosure);
        Assert.Equal(1, expected.VotersCount);
        Assert.Equal(64, expected.CandidateSnapshotDigestSha256?.Length);
        Assert.Equal(expected.CandidateSnapshotDigestSha256,
            reordered.CandidateSnapshotDigestSha256);
    }

    [Theory]
    [InlineData(AssemblyVoterEligibilityPolicy.Pending)]
    [InlineData("observado")]
    [InlineData("sin_revisar")]
    public void Pending_or_unrecognised_decisions_block_closure(string status)
    {
        var a = Assembly();
        var result = Run(a, Candidate(a, status));
        Assert.False(result.DocumentaryPreflightReady);
        Assert.Null(result.CandidateSnapshotDigestSha256);
        Assert.Contains("PENDING_OR_UNTRUSTED_ELIGIBILITY", result.BlockingReasons);
    }

    [Fact]
    public void Duplicate_person_or_missing_coverage_blocks_closure()
    {
        var a = Assembly();
        var one = Candidate(a);
        Assert.Contains("DUPLICATE_OR_INVALID_ASSEMBLY_MEMBER",
            Run(a, one, one).BlockingReasons);
        var missing = AssemblyClosurePreflightPolicy.Assess(
            a, [one], Coverage(a) with { FullEligibilityHistoryResolved = false }, Cutoff);
        Assert.Contains("OFFICIAL_ASSEMBLY_COVERAGE_MISSING", missing.BlockingReasons);
    }

    [Fact]
    public void No_office_source_or_no_financial_or_honor_evidence_fails_closed()
    {
        var a = Assembly();
        var row = Candidate(a);
        row.Member.SourceOfficeAssignmentId = Guid.Empty;
        Assert.Contains("MEMBER_OR_SOURCE_EVIDENCE_INCOMPLETE",
            Run(a, row).BlockingReasons);

        row = Candidate(a);
        var noTreasury = row with
        {
            Sources = row.Sources with { GrandTreasuryHistoryCertified = false }
        };
        Assert.Contains("MEMBER_OR_SOURCE_EVIDENCE_INCOMPLETE",
            Run(a, noTreasury).BlockingReasons);

        row = Candidate(a);
        var noHonor = row with
        {
            Sources = row.Sources with { HonorCoverageComplete = false }
        };
        Assert.Contains("MEMBER_OR_SOURCE_EVIDENCE_INCOMPLETE",
            Run(a, noHonor).BlockingReasons);
    }

    [Fact]
    public void False_financial_source_and_unresolved_old_debt_cannot_vote()
    {
        var a = Assembly();
        var row = Candidate(a);
        row.LatestEligibility.GrandTreasurySourceReference = "NO-CORRESPONDE";
        Assert.Contains("ELIGIBILITY_FINANCIAL_OR_RIGHTS_MISMATCH",
            Run(a, row).BlockingReasons);

        row = Candidate(a);
        row.LatestEligibility.GrandTreasuryPendingAmount = 1200m;
        Assert.Contains("UNRESOLVED_POSITIVE_TREASURY_DEBT",
            Run(a, row).BlockingReasons);

        var formallyExcepted = row with
        {
            Sources = row.Sources with { ValidDebtExceptionCertified = true }
        };
        Assert.True(Run(a, formallyExcepted).DocumentaryPreflightReady);
    }

    [Fact]
    public void Invalid_status_flags_or_wrong_cutoff_are_blocked()
    {
        var a = Assembly();
        var row = Candidate(a);
        row.LatestEligibility.MayVote = false;
        Assert.Contains("ELIGIBILITY_FINANCIAL_OR_RIGHTS_MISMATCH",
            Run(a, row).BlockingReasons);

        row = Candidate(a);
        var cutoff = row with
        {
            Sources = row.Sources with { CutoffDate = Cutoff.AddDays(-1) }
        };
        Assert.Contains("MEMBER_OR_SOURCE_EVIDENCE_INCOMPLETE",
            Run(a, cutoff).BlockingReasons);
    }

    [Fact]
    public void Future_cutoff_closed_assembly_and_empty_roster_never_pass()
    {
        var a = Assembly();
        a.CutoffDate = Cutoff.AddDays(1);
        Assert.Contains("ASSEMBLY_NOT_READY_FOR_CLOSURE",
            Run(a, Candidate(a)).BlockingReasons);

        a = Assembly();
        a.Status = "closed";
        Assert.Contains("ASSEMBLY_NOT_READY_FOR_CLOSURE",
            Run(a, Candidate(a)).BlockingReasons);

        a = Assembly();
        Assert.Contains("EMPTY_ASSEMBLY_ROSTER", Run(a).BlockingReasons);
    }

    [Fact]
    public void Changing_eligibility_changes_digest_without_personal_names()
    {
        var a = Assembly();
        var member = Candidate(a);
        var digest = Run(a, member).CandidateSnapshotDigestSha256;
        member.LatestEligibility.ReasonCode = "OTRO-FUNDAMENTO-VALIDADO";
        var changed = Run(a, member);
        Assert.True(changed.DocumentaryPreflightReady);
        Assert.NotEqual(digest, changed.CandidateSnapshotDigestSha256);
        Assert.DoesNotContain("Prueba", changed.CandidateSnapshotDigestSha256!);
    }
}
