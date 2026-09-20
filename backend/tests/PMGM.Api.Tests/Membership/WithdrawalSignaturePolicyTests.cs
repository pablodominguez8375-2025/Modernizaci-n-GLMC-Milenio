using PMGM.Api.Modules.Membership;
using Xunit;

namespace PMGM.Api.Tests.Membership;

public sealed class WithdrawalSignaturePolicyTests
{
    [Theory]
    [InlineData(WithdrawalSignaturePolicy.Venerable)]
    [InlineData(WithdrawalSignaturePolicy.Treasurer)]
    [InlineData(WithdrawalSignaturePolicy.Orator)]
    [InlineData(WithdrawalSignaturePolicy.Secretary)]
    public void AcceptsOnlyTheFourInstitutionalSignatories(string role)
        => Assert.True(WithdrawalSignaturePolicy.IsValidRole(role));

    [Fact]
    public void RequiresAllFourSignaturesBeforeExecution()
    {
        Assert.False(WithdrawalSignaturePolicy.IsComplete("vm", "tes", "ora", null));
        Assert.True(WithdrawalSignaturePolicy.IsComplete("vm", "tes", "ora", "sec"));
    }
}
