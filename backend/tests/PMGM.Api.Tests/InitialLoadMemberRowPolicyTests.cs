using PMGM.Api.Modules.Bootstrap.InitialLoadApply;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadMemberRowPolicyTests
{
    private static InitialLoadMemberRowPolicy.Row Ok(string p = "P1", string rut = "11.111.111-1") =>
        new(p, "RUT", rut, "Ana", "Pérez", "ana@example.org", "T1", "master", "active", "regular");

    [Fact]
    public void Valid_row_is_normalized_and_accepted()
    {
        var r = InitialLoadMemberRowPolicy.Inspect([Ok()]);
        Assert.True(r.Valid);
        Assert.Equal("11111111-1", r.Rows.Single().Rut);
    }

    [Theory]
    [InlineData("PASAPORTE", "AB123456", "IDENTITY_TYPE_UNSUPPORTED")]
    [InlineData("RUT", "11.111.111-2", "INVALID_RUT")]
    [InlineData("RUT", "", "INVALID_RUT")]
    public void Identity_is_fail_closed(string type, string number, string code)
    {
        var r = InitialLoadMemberRowPolicy.Inspect([Ok() with { IdentityType = type, IdentityNumber = number }]);
        Assert.False(r.Valid);
        Assert.Empty(r.Rows);
        Assert.Contains(r.Findings, f => f.Code == code);
    }

    [Theory]
    [InlineData("Maestro")]
    [InlineData("MASTER")]
    [InlineData("")]
    public void Grade_must_be_exact_canonical_code(string grade)
    {
        var r = InitialLoadMemberRowPolicy.Inspect([Ok() with { Grade = grade }]);
        Assert.False(r.Valid);
        Assert.Contains(r.Findings, f => f.Code == "GRADE_NOT_CANONICAL");
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("deceased")]
    [InlineData("Activo")]
    public void Only_active_status_is_admissible_without_inventing_events(string status)
    {
        var r = InitialLoadMemberRowPolicy.Inspect([Ok() with { InstitutionalStatus = status }]);
        Assert.False(r.Valid);
        Assert.Contains(r.Findings, f => f.Code == "STATUS_NOT_ADMISSIBLE");
    }

    [Fact]
    public void One_bad_row_blocks_the_whole_batch_and_duplicates_are_detected()
    {
        var r = InitialLoadMemberRowPolicy.Inspect([Ok("P1"), Ok("P1", "22.222.222-2"), Ok("P3")]);
        Assert.False(r.Valid);
        Assert.Empty(r.Rows);
        Assert.Contains(r.Findings, f => f.Code == "DUPLICATE_PERSON_REF");
        Assert.Contains(r.Findings, f => f.Code == "DUPLICATE_RUT_IN_BATCH");
    }

    [Fact]
    public void Findings_never_echo_submitted_values()
    {
        var r = InitialLoadMemberRowPolicy.Inspect([Ok() with { IdentityNumber = "SECRETO-99" }]);
        Assert.DoesNotContain(r.Findings, f => f.Code.Contains("SECRETO") || f.Column.Contains("SECRETO"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("honorary")]
    public void Membership_type_is_never_inferred(string type)
    {
        var r = InitialLoadMemberRowPolicy.Inspect([Ok() with { MembershipType = type }]);
        Assert.False(r.Valid);
        Assert.Contains(r.Findings, f => f.Code == "MEMBERSHIP_TYPE_NOT_EXPLICIT");
    }

    [Fact]
    public void Empty_batch_is_denied() =>
        Assert.False(InitialLoadMemberRowPolicy.Inspect([]).Valid);
}
