using System.Security.Claims;
using System.Text.Json;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadAdmissionPolicyTests
{
    private static readonly InstitutionalAccessService Access = new();

    private static ClaimsPrincipal Actor(string role, bool withOrderScope = true)
    {
        var claims = new List<Claim> { new(InstitutionalClaims.Role, role) };
        if (withOrderScope)
            claims.Add(new Claim(InstitutionalClaims.Scope, "order"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "unit-test"));
    }

    private static InitialLoadAdmissionPolicy.Evidence Complete() => new(
        MasterAccountVerifiedByServer: true,
        LegalApprovalVerifiedByServer: true,
        AuditSinkVerifiedByServer: true,
        AuditReason: "Autorización registrada en acta de prueba sintética",
        HasPendingTransactions: false,
        PreviewValid: true,
        PreviewFresh: true,
        IdempotencyReady: true,
        TransactionReady: true,
        ReversalReady: true);

    private static InitialLoadAdmissionPolicy.Decision Evaluate(
        InitialLoadAdmissionPolicy.Window window,
        InitialLoadAdmissionPolicy.Operation operation,
        InitialLoadAdmissionPolicy.Evidence? evidence = null,
        ClaimsPrincipal? actor = null) =>
        InitialLoadAdmissionPolicy.Evaluate(
            actor ?? Actor(InstitutionalRoles.PlatformSuperAdmin),
            Access, window, operation, evidence ?? Complete());

    [Theory]
    [InlineData(InstitutionalRoles.GranLogiaAdmin)]
    [InlineData(InstitutionalRoles.GranSecretaria)]
    [InlineData(InstitutionalRoles.TallerSecretaria)]
    [InlineData(InstitutionalRoles.TallerAdmin)]
    public void Institutional_officers_never_inherit_platform_master_load_authority(string role)
    {
        var result = Evaluate(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen, actor: Actor(role));
        Assert.False(result.PrerequisitesSatisfied);
        Assert.False(result.MayProposeTransition);
        Assert.Contains("MASTER_REQUIRED", result.Codes);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Master_without_order_scope_fails_closed()
    {
        var result = Evaluate(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen,
            actor: Actor(InstitutionalRoles.PlatformSuperAdmin, false));
        Assert.Contains("MASTER_REQUIRED", result.Codes);
        Assert.Null(result.ProposedNextWindow);
    }

    [Fact]
    public void Platform_role_is_insufficient_without_uniquely_verified_master_account()
    {
        // A support superadmin may have the role and order scope but is not
        // automatically the unique institutional initial-load Master admin.
        var result = Evaluate(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Complete() with { MasterAccountVerifiedByServer = false });
        Assert.Contains("MASTER_ACCOUNT_REQUIRED", result.Codes);
        Assert.False(result.PrerequisitesSatisfied);
        Assert.False(result.MayProposeTransition);
        Assert.Null(result.ProposedNextWindow);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Opening_only_proposes_window_transition_after_trusted_evidence()
    {
        var open = Evaluate(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen);
        Assert.True(open.PrerequisitesSatisfied);
        Assert.True(open.MayProposeTransition);
        Assert.Equal(InitialLoadAdmissionPolicy.Window.Open, open.ProposedNextWindow);
        Assert.False(open.CanApply);
        Assert.Equal(0, open.WritesPerformed);

        // Juridical validation occurs outside the product; its legacy flag
        // may not block an operational transition supported by audit evidence.
        var externallyReviewed = Evaluate(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Complete() with { LegalApprovalVerifiedByServer = false });
        Assert.True(externallyReviewed.PrerequisitesSatisfied);
        Assert.Equal(InitialLoadAdmissionPolicy.Window.Open,
            externallyReviewed.ProposedNextWindow);

        var noAudit = Evaluate(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Complete() with { AuditSinkVerifiedByServer = false });
        Assert.Contains("AUDIT_UNAVAILABLE", noAudit.Codes);
        Assert.False(noAudit.MayProposeTransition);
    }

    [Fact]
    public void Closing_is_irreversible_and_allows_revoked_legal_approval()
    {
        var closed = Evaluate(InitialLoadAdmissionPolicy.Window.Open,
            InitialLoadAdmissionPolicy.Operation.RequestClose,
            Complete() with { LegalApprovalVerifiedByServer = false });
        Assert.True(closed.MayProposeTransition);
        Assert.Equal(InitialLoadAdmissionPolicy.Window.Closed, closed.ProposedNextWindow);
        Assert.False(closed.CanApply);

        foreach (var action in Enum.GetValues<InitialLoadAdmissionPolicy.Operation>())
        {
            var terminal = Evaluate(InitialLoadAdmissionPolicy.Window.Closed, action);
            Assert.Contains("WINDOW_CLOSED", terminal.Codes);
            Assert.False(terminal.MayProposeTransition);
            Assert.False(terminal.MayPreview);
            Assert.False(terminal.CanApply);
        }
    }

    [Fact]
    public void Cannot_close_while_pending_transactions_or_without_audit_reason()
    {
        var pending = Evaluate(InitialLoadAdmissionPolicy.Window.Open,
            InitialLoadAdmissionPolicy.Operation.RequestClose,
            Complete() with { HasPendingTransactions = true });
        Assert.Contains("PENDING_TRANSACTIONS", pending.Codes);
        Assert.Null(pending.ProposedNextWindow);

        var missing = Evaluate(InitialLoadAdmissionPolicy.Window.Open,
            InitialLoadAdmissionPolicy.Operation.RequestClose,
            Complete() with { AuditReason = "too short" });
        Assert.Contains("AUDIT_REASON_REQUIRED", missing.Codes);
    }

    [Fact]
    public void Preview_requires_open_window_master_and_audit_not_juridical_gate()
    {
        var open = Evaluate(InitialLoadAdmissionPolicy.Window.Open,
            InitialLoadAdmissionPolicy.Operation.Preview);
        Assert.True(open.MayPreview);
        Assert.False(open.CanApply);
        Assert.False(Evaluate(InitialLoadAdmissionPolicy.Window.NotOpened,
            InitialLoadAdmissionPolicy.Operation.Preview).MayPreview);
        Assert.True(Evaluate(InitialLoadAdmissionPolicy.Window.Open,
            InitialLoadAdmissionPolicy.Operation.Preview,
            Complete() with { LegalApprovalVerifiedByServer = false }).MayPreview);
        Assert.False(Evaluate(InitialLoadAdmissionPolicy.Window.Open,
            InitialLoadAdmissionPolicy.Operation.Preview,
            Complete() with { AuditSinkVerifiedByServer = false }).MayPreview);
    }

    [Fact]
    public void Apply_is_never_enabled_even_if_every_prerequisite_is_satisfied()
    {
        var decision = Evaluate(InitialLoadAdmissionPolicy.Window.Open,
            InitialLoadAdmissionPolicy.Operation.RequestApply);
        Assert.True(decision.PrerequisitesSatisfied);
        Assert.Empty(decision.Codes);
        Assert.False(decision.MayProposeTransition);
        Assert.Null(decision.ProposedNextWindow);
        Assert.False(decision.CanApply);
        Assert.Equal(0, decision.WritesPerformed);
    }

    [Fact]
    public void Missing_required_checks_each_fail_closed()
    {
        var cases = new (InitialLoadAdmissionPolicy.Evidence Evidence, string Code)[]
        {
            (Complete() with { AuditSinkVerifiedByServer = false }, "AUDIT_UNAVAILABLE"),
            (Complete() with { PreviewValid = false }, "INVALID_PREFLIGHT"),
            (Complete() with { PreviewFresh = false }, "STALE_PREFLIGHT"),
            (Complete() with { IdempotencyReady = false }, "IDEMPOTENCY_NOT_READY"),
            (Complete() with { TransactionReady = false }, "TRANSACTION_NOT_READY"),
            (Complete() with { ReversalReady = false }, "REVERSAL_NOT_READY"),
            (Complete() with { HasPendingTransactions = true }, "PENDING_TRANSACTIONS"),
            (Complete() with { AuditReason = "private\ncontent" }, "AUDIT_REASON_REQUIRED")
        };
        foreach (var (evidence, code) in cases)
        {
            var result = Evaluate(InitialLoadAdmissionPolicy.Window.Open,
                InitialLoadAdmissionPolicy.Operation.RequestApply, evidence);
            Assert.False(result.PrerequisitesSatisfied);
            Assert.Contains(code, result.Codes);
            Assert.False(result.CanApply);
            Assert.Equal(0, result.WritesPerformed);
            Assert.DoesNotContain("private", JsonSerializer.Serialize(result));
        }
    }

    [Fact]
    public void Unknown_enum_values_fail_closed()
    {
        var decision = Evaluate((InitialLoadAdmissionPolicy.Window)999,
            InitialLoadAdmissionPolicy.Operation.RequestApply);
        Assert.Contains("INVALID_OPERATION_CONTEXT", decision.Codes);
        Assert.False(decision.CanApply);

        decision = Evaluate(InitialLoadAdmissionPolicy.Window.Open,
            (InitialLoadAdmissionPolicy.Operation)999);
        Assert.Contains("INVALID_OPERATION_CONTEXT", decision.Codes);
    }
}
