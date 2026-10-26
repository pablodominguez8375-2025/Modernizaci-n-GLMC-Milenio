using System.Security.Claims;
using System.Text.Json;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadWindowTransitionGuardTests
{
    private static readonly InstitutionalAccessService Access = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 19, 0, 0, TimeSpan.Zero);
    private static readonly Guid OpenAuditId = Guid.Parse("419911bc-a250-4a9a-99c5-15a36b09f9f9");
    private static readonly Guid CloseAuditId = Guid.Parse("6cd41d91-14a4-4ba8-af5f-1f9109159ce7");

    private static ClaimsPrincipal Master() =>
        new(new ClaimsIdentity(
            [
                new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin),
                new Claim(InstitutionalClaims.Scope, "order")
            ], "synthetic-unit-test"));

    private static InitialLoadAdmissionPolicy.Evidence Evidence() => new(
        MasterAccountVerifiedByServer: true,
        LegalApprovalVerifiedByServer: true,
        AuditSinkVerifiedByServer: true,
        AuditReason: "Aprobación ficticia para prueba aislada",
        HasPendingTransactions: false);

    private static InitialLoadAdmissionPolicy.Decision Admission(
        InitialLoadAdmissionPolicy.Window window,
        InitialLoadAdmissionPolicy.Operation action,
        InitialLoadAdmissionPolicy.Evidence? evidence = null) =>
        InitialLoadAdmissionPolicy.Evaluate(
            Master(), Access, window, action, evidence ?? Evidence());

    private static InitialLoadWindow Initial() => new()
    {
        CreatedAtUtc = Now.AddHours(-3),
        UpdatedAtUtc = Now.AddHours(-3)
    };

    private static InitialLoadWindow Opened() => new()
    {
        Status = InitialLoadWindow.Open,
        Version = 1,
        CreatedAtUtc = Now.AddHours(-3),
        UpdatedAtUtc = Now.AddHours(-2),
        OpenedAtUtc = Now.AddHours(-2),
        OpenedAuditEventId = OpenAuditId,
        LegalApprovalDigest = new string('a', 64)
    };

    [Fact]
    public void Opening_validated_server_decision_proposes_version_one_but_no_writes()
    {
        var result = InitialLoadWindowTransitionGuard.Plan(
            Initial(), InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Admission(InitialLoadAdmissionPolicy.Window.NotOpened,
                InitialLoadAdmissionPolicy.Operation.RequestOpen),
            Now, OpenAuditId, new string('c', 64));
        Assert.True(result.PreconditionsSatisfied);
        Assert.Equal(InitialLoadWindow.Open, result.ProposedStatus);
        Assert.Equal(0, result.ExpectedVersion);
        Assert.Equal(1, result.ProposedVersion);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
        Assert.Equal("TRANSITION_PREPARED", result.Code);
    }

    [Fact]
    public void Closing_validated_server_decision_proposes_terminal_version_two()
    {
        var result = InitialLoadWindowTransitionGuard.Plan(
            Opened(), InitialLoadAdmissionPolicy.Operation.RequestClose,
            Admission(InitialLoadAdmissionPolicy.Window.Open,
                InitialLoadAdmissionPolicy.Operation.RequestClose),
            Now, CloseAuditId);
        Assert.True(result.PreconditionsSatisfied);
        Assert.Equal(InitialLoadWindow.Closed, result.ProposedStatus);
        Assert.Equal(1, result.ExpectedVersion);
        Assert.Equal(2, result.ProposedVersion);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Missing_or_corrupt_window_cannot_be_opened_by_assumption()
    {
        var admission = Admission(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen);
        var missing = InitialLoadWindowTransitionGuard.Plan(
            null, InitialLoadAdmissionPolicy.Operation.RequestOpen, admission,
            Now, OpenAuditId, new string('a', 64));
        Assert.Equal("WINDOW_STATUS_UNAVAILABLE", missing.Code);

        var corrupt = Initial();
        corrupt.Version = 3;
        var bad = InitialLoadWindowTransitionGuard.Plan(
            corrupt, InitialLoadAdmissionPolicy.Operation.RequestOpen, admission,
            Now, OpenAuditId, new string('a', 64));
        Assert.Equal("WINDOW_STATUS_UNAVAILABLE", bad.Code);
    }

    [Fact]
    public void External_legal_review_is_not_a_gate_but_master_verification_is()
    {
        var outsideSoftware = Admission(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Evidence() with { LegalApprovalVerifiedByServer = false });
        var result = InitialLoadWindowTransitionGuard.Plan(
            Initial(), InitialLoadAdmissionPolicy.Operation.RequestOpen,
            outsideSoftware, Now, OpenAuditId, new string('a', 64));
        Assert.Equal("TRANSITION_PREPARED", result.Code);

        var noMaster = Admission(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Evidence() with { MasterAccountVerifiedByServer = false });
        result = InitialLoadWindowTransitionGuard.Plan(
            Initial(), InitialLoadAdmissionPolicy.Operation.RequestOpen,
            noMaster, Now, OpenAuditId, new string('a', 64));
        Assert.Equal("AUTHORIZATION_REQUIRED", result.Code);
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_legal_digest_fails_closed_without_echoing_value(string? digest)
    {
        var result = InitialLoadWindowTransitionGuard.Plan(
            Initial(), InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Admission(InitialLoadAdmissionPolicy.Window.NotOpened,
                InitialLoadAdmissionPolicy.Operation.RequestOpen),
            Now, OpenAuditId, digest);
        Assert.Equal("LEGAL_APPROVAL_REQUIRED", result.Code);
        Assert.False(result.CanApply);
        Assert.DoesNotContain("not-a-hash", JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Cannot_reopen_or_jump_or_reuse_an_old_admission_decision()
    {
        var goodOpen = Admission(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen);
        var result = InitialLoadWindowTransitionGuard.Plan(
            Opened(), InitialLoadAdmissionPolicy.Operation.RequestOpen,
            goodOpen, Now, CloseAuditId, new string('a', 64));
        Assert.Equal("INVALID_WINDOW_TRANSITION", result.Code);

        result = InitialLoadWindowTransitionGuard.Plan(
            Initial(), InitialLoadAdmissionPolicy.Operation.RequestClose,
            Admission(InitialLoadAdmissionPolicy.Window.Open,
                InitialLoadAdmissionPolicy.Operation.RequestClose),
            Now, CloseAuditId);
        Assert.Equal("INVALID_WINDOW_TRANSITION", result.Code);

        var closed = Opened();
        closed.Status = InitialLoadWindow.Closed;
        closed.Version = 2;
        closed.ClosedAtUtc = Now.AddHours(-1);
        closed.ClosedAuditEventId = CloseAuditId;
        result = InitialLoadWindowTransitionGuard.Plan(
            closed, InitialLoadAdmissionPolicy.Operation.RequestClose,
            Admission(InitialLoadAdmissionPolicy.Window.Open,
                InitialLoadAdmissionPolicy.Operation.RequestClose),
            Now, Guid.NewGuid());
        Assert.Equal("WINDOW_CLOSED", result.Code);
    }

    [Fact]
    public void Empty_or_reused_audit_event_and_non_utc_time_are_denied()
    {
        var open = Opened();
        var closeAdmission = Admission(InitialLoadAdmissionPolicy.Window.Open,
            InitialLoadAdmissionPolicy.Operation.RequestClose);
        Assert.Equal("INVALID_AUDIT_CONTEXT",
            InitialLoadWindowTransitionGuard.Plan(open,
                InitialLoadAdmissionPolicy.Operation.RequestClose, closeAdmission,
                Now, Guid.Empty).Code);
        Assert.Equal("INVALID_AUDIT_CONTEXT",
            InitialLoadWindowTransitionGuard.Plan(open,
                InitialLoadAdmissionPolicy.Operation.RequestClose, closeAdmission,
                Now, OpenAuditId).Code);
        Assert.Equal("INVALID_AUDIT_CONTEXT",
            InitialLoadWindowTransitionGuard.Plan(open,
                InitialLoadAdmissionPolicy.Operation.RequestClose, closeAdmission,
                Now.ToOffset(TimeSpan.FromHours(-3)), CloseAuditId).Code);
        Assert.Equal("INVALID_AUDIT_CONTEXT",
            InitialLoadWindowTransitionGuard.Plan(open,
                InitialLoadAdmissionPolicy.Operation.RequestClose, closeAdmission,
                Now.AddDays(-1), CloseAuditId).Code);
    }

    [Fact]
    public void Mismatched_operation_or_null_decision_never_prepares_transition()
    {
        var result = InitialLoadWindowTransitionGuard.Plan(
            Initial(), InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Admission(InitialLoadAdmissionPolicy.Window.Open,
                InitialLoadAdmissionPolicy.Operation.RequestClose),
            Now, OpenAuditId, new string('a', 64));
        Assert.Equal("INVALID_WINDOW_TRANSITION", result.Code);
        result = InitialLoadWindowTransitionGuard.Plan(
            Initial(), InitialLoadAdmissionPolicy.Operation.RequestApply,
            null, Now, OpenAuditId);
        Assert.Equal("INVALID_TRANSITION", result.Code);
    }
}
