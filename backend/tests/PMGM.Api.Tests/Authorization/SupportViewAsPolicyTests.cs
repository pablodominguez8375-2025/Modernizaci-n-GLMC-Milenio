using PMGM.Api.Modules.Authorization;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

public sealed class SupportViewAsPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private static SupportViewAsSession Session(
        string actor = "issuer|support-operator",
        string target = "issuer|lodge-user",
        string reason = "Caso de asistencia institucional QA-001",
        DateTimeOffset? started = null,
        DateTimeOffset? expires = null) => new(
            actor, target, reason, started ?? Now,
            expires ?? Now.AddMinutes(30));

    [Fact]
    public void Verified_actor_can_start_30_minute_view_as_an_unprivileged_other_user()
    {
        var result = SupportViewAsPolicy.EvaluateStart(
            Session(), operatorHasVerifiedReadSupportPermission: true,
            targetIsPrivileged: false, Now);
        Assert.True(result.Allowed);
        Assert.Equal("ALLOWED", result.Code);
    }

    [Theory]
    [InlineData(false, false, "SUPPORT_VIEW_PERMISSION_REQUIRED")]
    [InlineData(true, true, "SUPPORT_VIEW_PRIVILEGED_TARGET_DENIED")]
    public void Requires_verified_grant_and_non_privileged_target(
        bool granted, bool targetIsAdmin, string expectedCode)
    {
        var result = SupportViewAsPolicy.EvaluateStart(
            Session(), granted, targetIsAdmin, Now);
        Assert.False(result.Allowed);
        Assert.Equal(expectedCode, result.Code);
    }

    [Fact]
    public void No_self_view_as_or_blank_identity()
    {
        Assert.Equal("SUPPORT_VIEW_SELF_DENIED",
            SupportViewAsPolicy.EvaluateStart(
                Session(target: "issuer|support-operator"), true, false, Now).Code);
        Assert.Equal("SUPPORT_VIEW_IDENTITY_REQUIRED",
            SupportViewAsPolicy.EvaluateStart(
                Session(actor: "  "), true, false, Now).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("help")]
    [InlineData("           ")]
    [InlineData("Caso\nmalicioso")]
    public void Requires_a_specific_safe_reason(string reason)
    {
        Assert.Equal("SUPPORT_VIEW_REASON_REQUIRED",
            SupportViewAsPolicy.EvaluateStart(
                Session(reason: reason), true, false, Now).Code);
    }

    [Fact]
    public void Expiry_is_strictly_at_most_thirty_minutes_with_no_future_or_stale_sessions()
    {
        var tooLong = Session(expires: Now.AddMinutes(30).AddSeconds(1));
        var expired = Session(started: Now.AddMinutes(-30), expires: Now);
        var future = Session(started: Now.AddSeconds(1), expires: Now.AddMinutes(30));
        foreach (var candidate in new[] { tooLong, expired, future })
        {
            Assert.Equal("SUPPORT_VIEW_SESSION_EXPIRED_OR_INVALID",
                SupportViewAsPolicy.EvaluateStart(
                    candidate, true, false, Now).Code);
        }
        Assert.Equal(TimeSpan.FromMinutes(30), SupportViewAsPolicy.MaxLifetime);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    [InlineData(null)]
    public void A_support_view_as_session_never_authorizes_a_write_or_unknown_method(
        string? method)
    {
        var decision = Read(method: method);
        Assert.False(decision.Allowed);
        Assert.Equal("SUPPORT_VIEW_READ_ONLY", decision.Code);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public void Read_requires_existing_verified_permissions_and_an_explicit_endpoint_allowlist(
        string method)
    {
        Assert.True(Read(method: method).Allowed);
        Assert.Equal("SUPPORT_VIEW_RESOURCE_DENIED",
            Read(method: method, endpointAllowed: false).Code);
        Assert.Equal("SUPPORT_VIEW_RESOURCE_DENIED",
            Read(method: method, targetCanRead: false).Code);
        Assert.Equal("SUPPORT_VIEW_RESOURCE_DENIED",
            Read(method: method, restricted: true).Code);
        Assert.Equal("SUPPORT_VIEW_PERMISSION_REQUIRED",
            Read(method: method, verifiedGrant: false).Code);
    }

    [Fact]
    public void Cannot_reuse_another_operators_session_or_see_an_admin_after_role_changes()
    {
        Assert.Equal("SUPPORT_VIEW_OPERATOR_MISMATCH",
            Read(actor: "issuer|different-admin").Code);
        Assert.Equal("SUPPORT_VIEW_PRIVILEGED_TARGET_DENIED",
            Read(targetPrivileged: true).Code);
        Assert.Equal("SUPPORT_VIEW_SESSION_EXPIRED_OR_INVALID",
            Read(now: Now.AddMinutes(30)).Code);
    }

    private static SupportViewAsDecision Read(
        string? method = "GET",
        string actor = "issuer|support-operator",
        bool verifiedGrant = true,
        bool targetPrivileged = false,
        bool endpointAllowed = true,
        bool targetCanRead = true,
        bool restricted = false,
        DateTimeOffset? now = null) =>
        SupportViewAsPolicy.EvaluateRead(
            Session(), actor, method, verifiedGrant, targetPrivileged,
            endpointAllowed, targetCanRead, restricted, now ?? Now);
}
