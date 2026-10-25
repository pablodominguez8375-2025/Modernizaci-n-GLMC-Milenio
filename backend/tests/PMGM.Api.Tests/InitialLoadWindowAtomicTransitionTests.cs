using System.Security.Claims;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.Entities;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadWindowAtomicTransitionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 24, 20, 0, 0, TimeSpan.Zero);
    private static readonly Guid OpenEvent = Guid.Parse("48a7108e-20c0-4e45-95f0-cc203cab9df1");
    private static readonly Guid CloseEvent = Guid.Parse("8916d534-75e8-43ca-ae17-1b6ad4c845c0");
    private const string Digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static InitialLoadWindow Initial() => new()
    {
        CreatedAtUtc = Now.AddHours(-3),
        UpdatedAtUtc = Now.AddHours(-3)
    };

    private static ClaimsPrincipal Master() => new(new ClaimsIdentity(
    [
        new Claim(InstitutionalClaims.Role, InstitutionalRoles.PlatformSuperAdmin),
        new Claim(InstitutionalClaims.Scope, "order")
    ], "synthetic-unit-test"));

    private static InitialLoadAdmissionPolicy.Decision Admission(
        InitialLoadAdmissionPolicy.Window state,
        InitialLoadAdmissionPolicy.Operation operation) =>
        InitialLoadAdmissionPolicy.Evaluate(
            Master(), new InstitutionalAccessService(), state, operation,
            new InitialLoadAdmissionPolicy.Evidence(
                MasterAccountVerifiedByServer: true,
                LegalApprovalVerifiedByServer: true,
                AuditSinkVerifiedByServer: true,
                AuditReason: "Razón ficticia de autorización QA",
                HasPendingTransactions: false));

    private static (InitialLoadWindow Window, InitialLoadWindowTransitionGuard.PlanResult Plan)
        ValidOpening()
    {
        var window = Initial();
        var plan = InitialLoadWindowTransitionGuard.Plan(
            window, InitialLoadAdmissionPolicy.Operation.RequestOpen,
            Admission(InitialLoadAdmissionPolicy.Window.NotOpened,
                InitialLoadAdmissionPolicy.Operation.RequestOpen),
            Now, OpenEvent, Digest);
        Assert.True(plan.PreconditionsSatisfied);
        return (window, plan);
    }

    [Fact]
    public void Opening_materializes_version_one_and_preserves_original()
    {
        var (window, plan) = ValidOpening();
        var next = InitialLoadWindowAtomicTransition.Materialize(
            window, plan, Now, OpenEvent, Digest);
        Assert.NotNull(next);
        Assert.Equal(InitialLoadWindow.Open, next.Status);
        Assert.Equal(1, next.Version);
        Assert.Equal(OpenEvent, next.OpenedAuditEventId);
        Assert.Equal(Digest, next.LegalApprovalDigest);
        Assert.Equal(window.CreatedAtUtc, next.CreatedAtUtc);
        Assert.Null(next.ClosedAtUtc);
        Assert.Equal(InitialLoadWindow.NotOpened, window.Status);
        Assert.Equal(0, window.Version);
        Assert.Equal("OK", InitialLoadWindowStatusReader.Project(next).Code);
    }

    [Fact]
    public void Closing_preserves_legal_and_opening_proof_and_is_terminal()
    {
        var (window, plan) = ValidOpening();
        var opened = InitialLoadWindowAtomicTransition.Materialize(
            window, plan, Now, OpenEvent, Digest)!;
        var closedAt = Now.AddMinutes(15);
        var closePlan = InitialLoadWindowTransitionGuard.Plan(
            opened, InitialLoadAdmissionPolicy.Operation.RequestClose,
            Admission(InitialLoadAdmissionPolicy.Window.Open,
                InitialLoadAdmissionPolicy.Operation.RequestClose),
            closedAt, CloseEvent);
        var closed = InitialLoadWindowAtomicTransition.Materialize(
            opened, closePlan, closedAt, CloseEvent, null);
        Assert.NotNull(closed);
        Assert.Equal(InitialLoadWindow.Closed, closed.Status);
        Assert.Equal(2, closed.Version);
        Assert.Equal(opened.OpenedAtUtc, closed.OpenedAtUtc);
        Assert.Equal(opened.OpenedAuditEventId, closed.OpenedAuditEventId);
        Assert.Equal(opened.LegalApprovalDigest, closed.LegalApprovalDigest);
        Assert.Equal(CloseEvent, closed.ClosedAuditEventId);
        Assert.Equal("OK", InitialLoadWindowStatusReader.Project(closed).Code);
        Assert.Equal(InitialLoadWindow.Open, opened.Status);
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            closed, closePlan, closedAt.AddMinutes(1), Guid.NewGuid(), null));
    }

    [Fact]
    public void Forged_stale_and_malformed_plans_are_denied()
    {
        var (window, plan) = ValidOpening();
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan with { ExpectedVersion = 1 }, Now, OpenEvent, Digest));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan with { CanApply = true }, Now, OpenEvent, Digest));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan with { WritesPerformed = 1 }, Now, OpenEvent, Digest));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan with { Code = "NOT_PREPARED" }, Now, OpenEvent, Digest));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan with { ProposedVersion = 3 }, Now, OpenEvent, Digest));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan with { ProposedStatus = InitialLoadWindow.Closed }, Now, OpenEvent, Digest));
    }

    [Fact]
    public void Invalid_legal_digest_audit_and_clock_fail_closed()
    {
        var (window, plan) = ValidOpening();
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan, Now, Guid.Empty, Digest));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan, Now, OpenEvent, "not-legal-proof"));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan, Now, OpenEvent, null));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan, Now.ToOffset(TimeSpan.FromHours(-3)), OpenEvent, Digest));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            window, plan, window.CreatedAtUtc.AddMinutes(-1), OpenEvent, Digest));
    }

    [Fact]
    public void Closing_cannot_replace_legal_digest_or_reuse_open_audit()
    {
        var (window, plan) = ValidOpening();
        var opened = InitialLoadWindowAtomicTransition.Materialize(
            window, plan, Now, OpenEvent, Digest)!;
        var at = Now.AddMinutes(10);
        var closePlan = InitialLoadWindowTransitionGuard.Plan(
            opened, InitialLoadAdmissionPolicy.Operation.RequestClose,
            Admission(InitialLoadAdmissionPolicy.Window.Open,
                InitialLoadAdmissionPolicy.Operation.RequestClose), at, CloseEvent);
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            opened, closePlan, at, CloseEvent, Digest));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            opened, closePlan, at, OpenEvent, null));
        Assert.Null(InitialLoadWindowAtomicTransition.Materialize(
            null, closePlan, at, CloseEvent, null));
    }
}
