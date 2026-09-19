using PMGM.Api.Modules.LodgeManagement;
using Xunit;

namespace PMGM.Api.Tests.LodgeManagement;

public sealed class LodgeMeetingClosurePolicyTests
{
    private static readonly Guid ExtractId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AuthorizationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Regular_meeting_requires_extract_before_closure()
    {
        var missing = LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Held,
            null,
            null,
            null);
        Assert.Contains("Extracto de Acta", missing);

        var valid = LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Held,
            null,
            ExtractId,
            null);
        Assert.Null(valid);
    }

    [Fact]
    public void Ceremonial_meeting_requires_extract_and_grand_secretariat_authorization()
    {
        var onlyExtract = LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Held,
            "initiation",
            ExtractId,
            null);
        Assert.Contains("Plancha de Autorización", onlyExtract);

        var onlyAuthorization = LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Held,
            "initiation",
            null,
            AuthorizationId);
        Assert.Contains("Extracto de Acta", onlyAuthorization);

        var valid = LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Held,
            "initiation",
            ExtractId,
            AuthorizationId);
        Assert.Null(valid);
    }


    [Theory]
    [InlineData("affiliation")]
    [InlineData("incorporation")]
    public void Admission_ceremonies_require_extract_and_grand_secretariat_plancha(string ceremonyType)
    {
        Assert.Contains("Plancha de Autorización", LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Held,
            ceremonyType,
            ExtractId,
            null));

        Assert.Null(LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Held,
            ceremonyType,
            ExtractId,
            AuthorizationId));
    }

    [Fact]
    public void Meeting_must_be_realized_before_document_closure()
    {
        var scheduled = LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Scheduled,
            null,
            ExtractId,
            null);
        Assert.Contains("Realizada", scheduled);

        var cancelled = LodgeMeetingClosurePolicy.Validate(
            LodgeManagementCodes.MeetingStatus.Cancelled,
            null,
            ExtractId,
            null);
        Assert.Contains("cancelada", cancelled, StringComparison.OrdinalIgnoreCase);
    }
}
