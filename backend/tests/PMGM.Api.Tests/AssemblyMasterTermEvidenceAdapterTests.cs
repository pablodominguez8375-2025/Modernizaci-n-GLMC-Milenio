using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.RegimenInterior;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyMasterTermEvidenceAdapterTests
{
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid Lodge = Guid.NewGuid();
    private static readonly DateOnly Cutoff = new(2026, 10, 19);
    private static readonly DateTimeOffset VerifiedAt =
        new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);
    private const string OfficeCode = "venerable-maestro-canonical-test";

    private static OfficeAssignment Office(
        DateOnly start, DateOnly? end = null) => new()
    {
        MemberId = MemberId,
        OrganizationId = Lodge,
        OfficeType = OfficeCode,
        Period = "Período acreditado sintético",
        StartDate = start,
        EndDate = end,
        EvidenceReference = "VM-ACTA-FICTICIA"
    };

    private static AssemblyMasterTermAttestation Cert(
        OfficeAssignment office, bool completed = false,
        bool serving = false) =>
        new(office.Id, office.MemberId, office.OrganizationId,
            office.StartDate!.Value, office.EndDate, Cutoff,
            office.EvidenceReference!, "GRAN-SECRETARIA-CERTIFICACION-TEST",
            true, completed, serving, VerifiedAt.AddHours(-1));

    private static AssemblyMasterTermEvidenceResult Classify(
        OfficeAssignment[] office, AssemblyMasterTermAttestation[] certs,
        bool registryComplete = true) =>
        AssemblyMasterTermEvidenceAdapter.Classify(
            MemberId, Cutoff, VerifiedAt, OfficeCode, registryComplete,
            office, certs);

    [Fact]
    public void Certified_first_current_term_is_representative_not_permanent()
    {
        var first = Office(new DateOnly(2025, 12, 1));
        var result = Classify([first], [Cert(first, serving: true)]);
        Assert.True(result.OfficialCoverageValidated);
        Assert.Equal(AssemblyMemberCategoryPolicy.Representative, result.Category?.Category);
        Assert.False(result.Category?.CompletedTerms > 0);
    }

    [Fact]
    public void Past_fully_completed_office_and_reelection_remain_permanent()
    {
        var past = Office(new DateOnly(2020, 1, 1), new DateOnly(2021, 12, 31));
        var reelected = Office(new DateOnly(2026, 1, 1));
        var result = Classify(
            [reelected, past],
            [Cert(reelected, serving: true), Cert(past, completed: true)]);
        Assert.True(result.OfficialCoverageValidated);
        Assert.Equal(AssemblyMemberCategoryPolicy.PermanentAssemblyMember, result.Category?.Category);
        Assert.True(result.Category?.CurrentlyServing);
        Assert.Equal(1, result.Category?.CompletedTerms);
    }

    [Fact]
    public void Missing_certification_or_official_coverage_fails_closed()
    {
        var past = Office(new DateOnly(2020, 1, 1), new DateOnly(2021, 12, 31));
        var missing = Classify([past], []);
        Assert.False(missing.OfficialCoverageValidated);
        Assert.Null(missing.Category);
        Assert.Contains("MASTER_TERM_NOT_ACCREDITED", missing.BlockingReasons);

        var noRegistry = Classify([past], [Cert(past, completed: true)], registryComplete: false);
        Assert.Contains("CANONICAL_MASTER_OFFICE_SOURCE_MISSING", noRegistry.BlockingReasons);
    }

    [Fact]
    public void End_date_alone_is_not_proof_of_full_term()
    {
        var ended = Office(new DateOnly(2020, 1, 1), new DateOnly(2021, 12, 31));
        var result = Classify([ended], [Cert(ended, completed: false)]);
        Assert.True(result.OfficialCoverageValidated);
        Assert.False(result.Category!.Qualifies);
    }

    [Fact]
    public void Wrong_member_workshop_or_evidence_link_is_rejected()
    {
        var first = Office(new DateOnly(2026, 1, 1));
        var correct = Cert(first, serving: true);
        foreach (var changed in new[]
        {
            correct with { MemberId = Guid.NewGuid() },
            correct with { OrganizationId = Guid.NewGuid() },
            correct with { OfficeEvidenceReference = "OTRA-ACTA" },
            correct with { EvidenceCutoff = Cutoff.AddDays(-1) },
            correct with { CertifiedAtUtc = VerifiedAt.AddDays(1) },
            correct with { InstitutionallyVerified = false }
        })
        {
            Assert.False(Classify([first], [changed]).OfficialCoverageValidated);
        }
    }

    [Fact]
    public void Duplicate_or_foreign_attestation_is_never_interpreted_as_two_terms()
    {
        var first = Office(new DateOnly(2026, 1, 1));
        var cert = Cert(first, serving: true);
        Assert.Contains("DUPLICATED_MASTER_OFFICE_EVIDENCE",
            Classify([first], [cert, cert]).BlockingReasons);
        Assert.Contains("FOREIGN_OR_UNRELATED_ATTESTATION",
            Classify([first], [cert with { OfficeAssignmentId = Guid.NewGuid() }])
                .BlockingReasons);
    }

    [Fact]
    public void Current_term_with_unverified_service_cannot_be_assumed_current()
    {
        var current = Office(new DateOnly(2026, 1, 1));
        var result = Classify([current], [Cert(current, serving: false)]);
        Assert.False(result.OfficialCoverageValidated);
        Assert.Null(result.Category);
    }
}
