using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadPreflightReceiptPolicyTests
{
    [Fact]
    public void Missing_key_or_csv_cannot_issue_a_receipt()
    {
        var utc = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        Assert.Null(InitialLoadPreflightReceiptPolicy.Issue(null, null, utc));
        Assert.Null(InitialLoadPreflightReceiptPolicy.Issue(null, new byte[32], utc));
        Assert.Null(InitialLoadPreflightReceiptPolicy.Issue(
            new Dictionary<string, string>(), new byte[31], utc));
    }

    [Fact]
    public void Untrusted_token_fails_closed()
    {
        var utc = new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero);
        var result = InitialLoadPreflightReceiptPolicy.Verify(null, "invalid", new byte[32], utc);
        Assert.False(result.Valid);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }
}
