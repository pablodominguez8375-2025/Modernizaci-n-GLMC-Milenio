using PMGM.Api.Modules.Privacy;
using Xunit;

namespace PMGM.Api.Tests.Privacy;

public sealed class PrivacyCodesTests
{
    [Theory]
    [InlineData(PrivacyCodes.DataSubjectRight.Access)]
    [InlineData(PrivacyCodes.DataSubjectRight.Rectification)]
    [InlineData(PrivacyCodes.DataSubjectRight.Erasure)]
    [InlineData(PrivacyCodes.DataSubjectRight.Objection)]
    [InlineData(PrivacyCodes.DataSubjectRight.Portability)]
    [InlineData(PrivacyCodes.DataSubjectRight.Blocking)]
    public void DataSubjectRights_AreAccepted(string value)
        => Assert.True(PrivacyCodes.DataSubjectRight.IsValid(value));

    [Fact]
    public void UnknownDataSubjectRight_IsRejected()
        => Assert.False(PrivacyCodes.DataSubjectRight.IsValid("unknown"));

    [Theory]
    [InlineData(PrivacyCodes.RetentionAction.Keep)]
    [InlineData(PrivacyCodes.RetentionAction.Review)]
    [InlineData(PrivacyCodes.RetentionAction.Anonymize)]
    [InlineData(PrivacyCodes.RetentionAction.Delete)]
    public void RetentionActions_AreAccepted(string value)
        => Assert.True(PrivacyCodes.RetentionAction.IsValid(value));
}
