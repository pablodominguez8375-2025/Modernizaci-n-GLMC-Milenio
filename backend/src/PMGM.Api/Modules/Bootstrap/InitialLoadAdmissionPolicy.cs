using System.Security.Claims;
using PMGM.Api.Modules.Authorization;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Pure fail-closed decision support for a FUTURE initial-load workflow.
/// It does not persist transitions or authorize actual execution. All evidence
/// flags must originate from trusted server-side records when later integrated;
/// never bind them from untrusted request JSON.
/// </summary>
public static class InitialLoadAdmissionPolicy
{
    public enum Window { NotOpened, Open, Closed }
    public enum Operation { RequestOpen, Preview, RequestApply, RequestClose }

    public sealed record Evidence(
        bool LegalApprovalVerifiedByServer = false,
        bool AuditSinkVerifiedByServer = false,
        string? AuditReason = null,
        bool HasPendingTransactions = false,
        bool PreviewValid = false,
        bool PreviewFresh = false,
        bool IdempotencyReady = false,
        bool TransactionReady = false,
        bool ReversalReady = false);

    public sealed record Decision(
        bool PrerequisitesSatisfied,
        bool MayProposeTransition,
        Window? ProposedNextWindow,
        bool MayPreview,
        bool CanApply,
        int WritesPerformed,
        IReadOnlyList<string> Codes);

    public static Decision Evaluate(
        ClaimsPrincipal? actor,
        IInstitutionalAccessService? access,
        Window window,
        Operation operation,
        Evidence? evidence)
    {
        var codes = new List<string>();
        void Fail(string code) => codes.Add(code);

        Decision Denied() => new(
            false, false, null, false, false, 0, codes.ToArray());

        if (actor is null || access is null || !access.IsPlatformSuperAdmin(actor))
        {
            Fail("MASTER_REQUIRED");
            return Denied();
        }

        if (!Enum.IsDefined(window) || !Enum.IsDefined(operation) || evidence is null)
        {
            Fail("INVALID_OPERATION_CONTEXT");
            return Denied();
        }

        // Terminal closure is irreversible in the initial-load process.
        // A new enrollment campaign requires a distinct protocol and issue.
        if (window == Window.Closed)
        {
            Fail("WINDOW_CLOSED");
            return Denied();
        }

        if (operation == Operation.RequestOpen)
        {
            if (window != Window.NotOpened) Fail("INVALID_WINDOW_TRANSITION");
            if (!evidence.LegalApprovalVerifiedByServer) Fail("LEGAL_APPROVAL_REQUIRED");
            ValidateAudit(evidence, Fail);

            var ready = codes.Count == 0;
            return new(ready, ready, ready ? Window.Open : null,
                false, false, 0, codes.ToArray());
        }

        if (window != Window.Open)
        {
            Fail("WINDOW_NOT_OPEN");
            return Denied();
        }

        if (operation == Operation.RequestClose)
        {
            // Closing is still allowed if the approval has been withdrawn:
            // closing the window reduces risk rather than enabling writes.
            ValidateAudit(evidence, Fail);
            if (evidence.HasPendingTransactions) Fail("PENDING_TRANSACTIONS");

            var ready = codes.Count == 0;
            return new(ready, ready, ready ? Window.Closed : null,
                false, false, 0, codes.ToArray());
        }

        if (!evidence.LegalApprovalVerifiedByServer)
            Fail("LEGAL_APPROVAL_REQUIRED");

        if (operation == Operation.Preview)
        {
            if (!evidence.AuditSinkVerifiedByServer)
                Fail("AUDIT_UNAVAILABLE");
            var ready = codes.Count == 0;
            return new(ready, false, null, ready, false, 0, codes.ToArray());
        }

        // RequestApply remains a REVIEW of prerequisites, never an executor.
        ValidateAudit(evidence, Fail);
        if (!evidence.PreviewValid) Fail("INVALID_PREFLIGHT");
        if (!evidence.PreviewFresh) Fail("STALE_PREFLIGHT");
        if (!evidence.IdempotencyReady) Fail("IDEMPOTENCY_NOT_READY");
        if (!evidence.TransactionReady) Fail("TRANSACTION_NOT_READY");
        if (!evidence.ReversalReady) Fail("REVERSAL_NOT_READY");
        if (evidence.HasPendingTransactions) Fail("PENDING_TRANSACTIONS");

        var prerequisites = codes.Count == 0;
        // Even with all server-verified proofs the system has no APPLY path.
        return new(prerequisites, false, null, false, false, 0, codes.ToArray());
    }

    private static void ValidateAudit(Evidence evidence, Action<string> fail)
    {
        if (!evidence.AuditSinkVerifiedByServer)
            fail("AUDIT_UNAVAILABLE");

        var reason = evidence.AuditReason?.Trim();
        if (reason is null || reason.Length is < 10 or > 500 ||
            reason.Any(char.IsControl))
            fail("AUDIT_REASON_REQUIRED");
        // Never echo the reason, even when it contains sensitive content.
    }
}
