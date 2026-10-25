using PMGM.Api.Modules.RegimenInterior;
using PMGM.Api.Modules.RegimenInterior.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class AssemblyAccreditedMemberMaterializationPolicyTests
{
    private static readonly Guid AssemblyId = Guid.NewGuid();
    private static readonly Guid CurrentWorkshopId = Guid.NewGuid();
    private static readonly Guid HistoricalWorkshopId = Guid.NewGuid();
    private static readonly DateOnly Cutoff = new(2026, 10, 23);
    private static readonly DateTimeOffset VerifiedAt =
        new(2026, 10, 23, 14, 0, 0, TimeSpan.Zero);

    private static AssemblyRecord Assembly() => new()
    {
        Id = AssemblyId,
        Name = "Asamblea sintética",
        MeetingDate = Cutoff.AddDays(3),
        CutoffDate = Cutoff,
        CreatedBySubject = "ri-test"
    };

    private static AssemblyMasterTermEvidenceResult Master(
        string? category = AssemblyMemberCategoryPolicy.Representative,
        bool coverage = true,
        bool qualified = true,
        Guid? officeId = null,
        bool sourceAvailable = true)
    {
        var selected = sourceAvailable && qualified
            ? new AssemblyAccreditedOfficeSource(
                officeId ?? Guid.NewGuid(), HistoricalWorkshopId,
                "CERTIFICACION-V.M.-SINTETICA",
                new DateOnly(2024, 1, 1),
                category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember
                    ? new DateOnly(2025, 12, 31) : null)
            : null;
        return new AssemblyMasterTermEvidenceResult(
            coverage,
            new AssemblyMemberCategoryDecision(
                category, qualified,
                category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember ? 1 : 0,
                category == AssemblyMemberCategoryPolicy.Representative,
                "Certificación de test"),
            [], selected);
    }

    private static AssemblyVerifiedMemberIdentity Member(
        AssemblyMasterTermEvidenceResult? term = null,
        Guid? personId = null, Guid? memberId = null,
        bool active = true) => new(
            personId ?? Guid.NewGuid(), memberId ?? Guid.NewGuid(),
            CurrentWorkshopId, Cutoff, Cutoff, Cutoff,
            true, true, active, "PERSONA-FUENTE-TEST",
            "AFILIACION-FUENTE-TEST", "Ana", "Pérez",
            "12.345.678-5", "Taller 23 - sintético",
            term ?? Master());

    private static AssemblyAccreditedMemberMaterializationResult Prepare(
        params AssemblyVerifiedMemberIdentity[] sources) =>
        AssemblyAccreditedMemberMaterializationPolicy.Prepare(
            Assembly(), sources, true, VerifiedAt);

    [Fact]
    public void Accredited_representative_uses_current_workshop_and_certified_office_source()
    {
        var source = Member();
        var result = Prepare(source);
        Assert.True(result.Prepared);
        var row = Assert.Single(result.Members);
        Assert.Equal(AssemblyId, row.AssemblyId);
        Assert.Equal(source.PersonId, row.PersonId);
        Assert.Equal(source.MemberId, row.MemberId);
        Assert.Equal(CurrentWorkshopId, row.OrganizationId);
        Assert.Equal(source.MasterTerm.AccreditedSource!.OfficeAssignmentId,
            row.SourceOfficeAssignmentId);
        Assert.Equal("CERTIFICACION-V.M.-SINTETICA", row.SourceEvidenceReference);
        Assert.Equal(AssemblyMemberCategoryPolicy.Representative, row.Category);
        Assert.False(row.FullTermVerified);
        Assert.Equal("Pérez", row.LastNamesAtCutoff);
        Assert.Equal(VerifiedAt, row.VerifiedAtUtc);
    }

    [Fact]
    public void Completed_past_period_survives_transfer_without_confusing_workshops()
    {
        var source = Member(Master(AssemblyMemberCategoryPolicy.PermanentAssemblyMember));
        var result = Prepare(source);
        var row = Assert.Single(result.Members);
        Assert.True(row.FullTermVerified);
        Assert.Equal(AssemblyMemberCategoryPolicy.PermanentAssemblyMember, row.Category);
        Assert.Equal(CurrentWorkshopId, row.OrganizationId);
        Assert.NotEqual(HistoricalWorkshopId, row.OrganizationId);
        Assert.Equal(source.MasterTerm.AccreditedSource!.OfficeAssignmentId,
            row.SourceOfficeAssignmentId);
    }

    [Fact]
    public void Inactive_and_non_accredited_persons_are_excluded_without_conferring_vote()
    {
        var inactive = Member(active: false);
        var notQualified = Member(Master(category: null, qualified: false,
            sourceAvailable: false));
        var valid = Member();
        var result = Prepare(inactive, notQualified, valid);
        Assert.True(result.Prepared);
        Assert.Equal(valid.PersonId, Assert.Single(result.Members).PersonId);
    }

    [Fact]
    public void Incomplete_official_coverage_denies_entire_batch()
    {
        var result = AssemblyAccreditedMemberMaterializationPolicy.Prepare(
            Assembly(), [Member()], false, VerifiedAt);
        Assert.False(result.Prepared);
        Assert.Empty(result.Members);
        Assert.Contains("INSTITUTIONAL_MEMBERSHIP_COVERAGE_MISSING", result.BlockingReasons);
    }

    [Fact]
    public void One_invalid_certification_fails_closed_for_all_members()
    {
        var result = Prepare(Member(), Member(Master(coverage: false)));
        Assert.False(result.Prepared);
        Assert.Empty(result.Members);
        Assert.Contains("MASTER_OFFICE_COVERAGE_UNVERIFIED", result.BlockingReasons);
    }

    [Fact]
    public void Duplicate_person_or_office_source_cannot_produce_partial_roster()
    {
        var first = Member();
        var duplicatePerson = Member(personId: first.PersonId);
        Assert.Contains("DUPLICATE_OR_INVALID_PERSON_SOURCE",
            Prepare(first, duplicatePerson).BlockingReasons);
        var sameOffice = first.MasterTerm.AccreditedSource!.OfficeAssignmentId;
        var duplicateOffice = Member(Master(officeId: sameOffice));
        var result = Prepare(first, duplicateOffice);
        Assert.False(result.Prepared);
        Assert.Empty(result.Members);
        Assert.Contains("ACCREDITED_MASTER_OFFICE_INVALID", result.BlockingReasons);
    }

    [Fact]
    public void Cutoff_mismatch_identity_mismatch_and_missing_source_are_denied()
    {
        var source = Member();
        var mismatches = new[]
        {
            source with { IdentityEvidenceCutoff = Cutoff.AddDays(-1) },
            source with { MembershipEvidenceCutoff = Cutoff.AddDays(1) },
            source with { CutoffDate = Cutoff.AddDays(-1) },
            source with { IdentityVerified = false },
            source with { MembershipVerified = false },
            source with { MembershipEvidenceReference = " " }
        };
        foreach (var changed in mismatches)
        {
            var result = Prepare(changed);
            Assert.False(result.Prepared);
            Assert.Empty(result.Members);
            Assert.Contains("MEMBERSHIP_OR_IDENTITY_SOURCE_UNVERIFIED",
                result.BlockingReasons);
        }
    }

    [Fact]
    public void Missing_source_data_or_certificate_reference_prevents_materialization()
    {
        var noOffice = Member(Master(sourceAvailable: false));
        var invalidName = Member() with { FirstNames = " " };
        Assert.Contains("ACCREDITED_MASTER_OFFICE_INVALID",
            Prepare(noOffice).BlockingReasons);
        Assert.Contains("PERSON_ROSTER_FIELDS_NOT_VERIFIED",
            Prepare(invalidName).BlockingReasons);
    }

    [Fact]
    public void Assembly_closed_empty_or_only_unqualified_blocks_projection()
    {
        var assembly = Assembly();
        assembly.Status = "closed";
        var closed = AssemblyAccreditedMemberMaterializationPolicy.Prepare(
            assembly, [Member()], true, VerifiedAt);
        Assert.False(closed.Prepared);
        Assert.Contains("ASSEMBLY_NOT_EDITABLE", closed.BlockingReasons);
        Assert.Contains("INVALID_SOURCE_ROSTER_SIZE", Prepare().BlockingReasons);
        Assert.Contains("NO_ACCREDITED_ASSEMBLY_MEMBERS",
            Prepare(Member(Master(category: null, qualified: false,
                sourceAvailable: false))).BlockingReasons);
    }
}
