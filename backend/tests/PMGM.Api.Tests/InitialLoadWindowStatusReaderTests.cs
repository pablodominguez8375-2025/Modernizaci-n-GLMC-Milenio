using System.Text.Json;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadWindowStatusReaderTests
{
    private static InitialLoadWindow OpenWindow()
        => new()
        {
            Id = InitialLoadWindow.SingletonId,
            Status = InitialLoadWindow.Open,
            Version = 1,
            LegalApprovalDigest = new string('a', 64),
            OpenedAtUtc = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero),
            OpenedAuditEventId = Guid.Parse("7c7ad94b-91e4-4a70-ac34-cdf024b41311")
        };

    [Fact]
    public void Seed_state_is_readable_but_always_denies_apply()
    {
        var view = InitialLoadWindowStatusReader.Project(new InitialLoadWindow());
        Assert.Equal(InitialLoadWindow.NotOpened, view.Status);
        Assert.Equal(0, view.Version);
        Assert.Equal("OK", view.Code);
        Assert.False(view.CanApply);
        Assert.Equal(0, view.WritesPerformed);
    }

    [Fact]
    public void Valid_open_and_closed_only_return_state_and_version()
    {
        var opened = OpenWindow();
        var view = InitialLoadWindowStatusReader.Project(opened);
        Assert.Equal(InitialLoadWindow.Open, view.Status);
        Assert.Equal(1, view.Version);
        Assert.False(view.CanApply);

        opened.Status = InitialLoadWindow.Closed;
        opened.Version = 2;
        opened.ClosedAtUtc = opened.OpenedAtUtc!.Value.AddMinutes(3);
        opened.ClosedAuditEventId = Guid.Parse("96b0b36b-0b2f-45f3-934b-9054a2efda21");
        view = InitialLoadWindowStatusReader.Project(opened);
        Assert.Equal(InitialLoadWindow.Closed, view.Status);
        Assert.Equal(2, view.Version);
        Assert.False(view.CanApply);
        Assert.DoesNotContain(opened.LegalApprovalDigest!, JsonSerializer.Serialize(view));
        Assert.DoesNotContain(opened.OpenedAuditEventId!.Value.ToString(), JsonSerializer.Serialize(view));
        Assert.DoesNotContain(opened.ClosedAuditEventId!.Value.ToString(), JsonSerializer.Serialize(view));
    }

    [Fact]
    public void Missing_window_is_never_assumed_open()
    {
        var view = InitialLoadWindowStatusReader.Project(null);
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable, view.Status);
        Assert.Null(view.Version);
        Assert.Equal("WINDOW_STATUS_UNAVAILABLE", view.Code);
        Assert.False(view.CanApply);
        Assert.Equal(0, view.WritesPerformed);
    }

    [Theory]
    [InlineData(InitialLoadWindow.Open, 0)]
    [InlineData(InitialLoadWindow.Closed, 1)]
    [InlineData("corrupted", 1)]
    [InlineData(InitialLoadWindow.NotOpened, 1)]
    public void Invalid_state_and_version_fail_closed(string status, long version)
    {
        var value = OpenWindow();
        value.Status = status;
        value.Version = version;
        var result = InitialLoadWindowStatusReader.Project(value);
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable, result.Status);
        Assert.False(result.CanApply);
    }

    [Fact]
    public void Missing_legal_audit_or_bad_digest_never_exposes_an_open_window()
    {
        var value = OpenWindow();
        value.LegalApprovalDigest = null;
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable,
            InitialLoadWindowStatusReader.Project(value).Status);

        value.LegalApprovalDigest = new string('a', 64);
        value.OpenedAuditEventId = null;
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable,
            InitialLoadWindowStatusReader.Project(value).Status);

        value.OpenedAuditEventId = Guid.NewGuid();
        value.LegalApprovalDigest = new string('Z', 64);
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable,
            InitialLoadWindowStatusReader.Project(value).Status);
    }

    [Fact]
    public void Closing_before_opening_or_reusing_same_audit_fails_closed()
    {
        var value = OpenWindow();
        value.Status = InitialLoadWindow.Closed;
        value.Version = 2;
        value.ClosedAuditEventId = value.OpenedAuditEventId;
        value.ClosedAtUtc = value.OpenedAtUtc!.Value.AddSeconds(-1);
        var view = InitialLoadWindowStatusReader.Project(value);
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable, view.Status);

        value.ClosedAtUtc = value.OpenedAtUtc!.Value.AddSeconds(1);
        view = InitialLoadWindowStatusReader.Project(value);
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable, view.Status);
    }

    [Fact]
    public void Corrupted_timestamp_and_wrong_singleton_id_fail_closed()
    {
        var value = new InitialLoadWindow();
        value.UpdatedAtUtc = value.CreatedAtUtc.AddHours(-1);
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable,
            InitialLoadWindowStatusReader.Project(value).Status);
        value.UpdatedAtUtc = value.CreatedAtUtc;
        value.Id = 2;
        Assert.Equal(InitialLoadWindowStatusReader.Unavailable,
            InitialLoadWindowStatusReader.Project(value).Status);
    }
}
