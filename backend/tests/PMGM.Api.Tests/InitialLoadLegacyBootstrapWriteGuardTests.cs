using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadLegacyBootstrapWriteGuardTests
{
    [Fact]
    public void Unopened_seed_allows_legacy_institutional_bootstrap_only()
    {
        Assert.True(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(new InitialLoadWindow()));
    }

    [Fact]
    public void Opened_window_blocks_all_legacy_institutional_writes()
    {
        var window = new InitialLoadWindow
        {
            Status = InitialLoadWindow.Open,
            Version = 1,
            LegalApprovalDigest = new string('a', 64),
            OpenedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
            OpenedAuditEventId = Guid.NewGuid()
        };
        Assert.False(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(window));
    }

    [Fact]
    public void Closed_window_remains_read_only_even_when_audit_is_valid()
    {
        var opened = DateTimeOffset.UtcNow.AddHours(-2);
        var window = new InitialLoadWindow
        {
            Status = InitialLoadWindow.Closed,
            Version = 2,
            LegalApprovalDigest = new string('b', 64),
            OpenedAtUtc = opened,
            OpenedAuditEventId = Guid.NewGuid(),
            ClosedAtUtc = opened.AddMinutes(10),
            ClosedAuditEventId = Guid.NewGuid()
        };
        Assert.False(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(window));
    }

    [Fact]
    public void Missing_inconsistent_or_unknown_window_fails_closed()
    {
        Assert.False(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(null));
        Assert.False(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(
            new InitialLoadWindow { Status = InitialLoadWindow.NotOpened, Version = 1 }));
        Assert.False(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(
            new InitialLoadWindow { Status = "unknown" }));
        Assert.False(InitialLoadLegacyBootstrapWriteGuard.AllowsApply(
            new InitialLoadWindow
            {
                Status = InitialLoadWindow.NotOpened,
                LegalApprovalDigest = new string('a', 64)
            }));
    }
}
