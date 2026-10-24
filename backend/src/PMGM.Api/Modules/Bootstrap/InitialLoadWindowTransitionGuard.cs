using PMGM.Api.Modules.Bootstrap.Entities;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Pure contract for a FUTURE atomic and audited window transition.
/// Inputs must be server-verified, never bound from HTTP request JSON.
/// This class cannot persist, open, close or import anything.
/// </summary>
public static class InitialLoadWindowTransitionGuard
{
    public sealed record PlanResult(
        bool PreconditionsSatisfied,
        string? ProposedStatus,
        long? ExpectedVersion,
        long? ProposedVersion,
        bool CanApply,
        int WritesPerformed,
        string Code);

    public static PlanResult Plan(
        InitialLoadWindow? existing,
        InitialLoadAdmissionPolicy.Operation requested,
        InitialLoadAdmissionPolicy.Decision? trustedAdmission,
        DateTimeOffset nowUtc,
        Guid proposedAuditEventId,
        string? serverVerifiedLegalDigest = null)
    {
        static PlanResult Denied(string code) =>
            new(false, null, null, null, false, 0, code);

        // Reader checks the exact DB CHECK invariants before considering any
        // state change. A missing or corrupted row cannot be assumed unopened.
        if (existing is null ||
            InitialLoadWindowStatusReader.Project(existing).Status == InitialLoadWindowStatusReader.Unavailable)
            return Denied("WINDOW_STATUS_UNAVAILABLE");

        if (requested is not (InitialLoadAdmissionPolicy.Operation.RequestOpen
            or InitialLoadAdmissionPolicy.Operation.RequestClose))
            return Denied("INVALID_TRANSITION");

        if (trustedAdmission is null ||
            !trustedAdmission.PrerequisitesSatisfied ||
            !trustedAdmission.MayProposeTransition ||
            trustedAdmission.Codes.Count != 0 ||
            trustedAdmission.CanApply ||
            trustedAdmission.WritesPerformed != 0)
            return Denied("AUTHORIZATION_REQUIRED");

        if (proposedAuditEventId == Guid.Empty ||
            nowUtc.Offset != TimeSpan.Zero ||
            nowUtc < existing.CreatedAtUtc || nowUtc < existing.UpdatedAtUtc)
            return Denied("INVALID_AUDIT_CONTEXT");

        if (requested == InitialLoadAdmissionPolicy.Operation.RequestOpen)
        {
            if (existing.Status != InitialLoadWindow.NotOpened ||
                existing.Version != 0 ||
                trustedAdmission.ProposedNextWindow != InitialLoadAdmissionPolicy.Window.Open)
                return Denied("INVALID_WINDOW_TRANSITION");

            if (!ValidDigest(serverVerifiedLegalDigest))
                return Denied("LEGAL_APPROVAL_REQUIRED");

            return new PlanResult(true, InitialLoadWindow.Open, 0, 1, false, 0, "TRANSITION_PREPARED");
        }

        if (existing.Status != InitialLoadWindow.Open || existing.Version != 1 ||
            trustedAdmission.ProposedNextWindow != InitialLoadAdmissionPolicy.Window.Closed)
            return Denied(existing.Status == InitialLoadWindow.Closed
                ? "WINDOW_CLOSED" : "INVALID_WINDOW_TRANSITION");

        if (!existing.OpenedAtUtc.HasValue ||
            nowUtc < existing.OpenedAtUtc.Value ||
            existing.OpenedAuditEventId == proposedAuditEventId)
            return Denied("INVALID_AUDIT_CONTEXT");

        // Legal digest and opening audit proof must remain unchanged on close.
        // A separate transactional service will persist both the closing
        // audit event and the state update together.
        return new PlanResult(true, InitialLoadWindow.Closed, 1, 2, false, 0, "TRANSITION_PREPARED");
    }

    private static bool ValidDigest(string? digest)
    {
        if (digest?.Length != 64) return false;
        foreach (var c in digest)
            if (c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        return true;
    }
}
