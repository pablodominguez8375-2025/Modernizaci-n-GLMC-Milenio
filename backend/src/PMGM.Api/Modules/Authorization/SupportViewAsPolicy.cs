namespace PMGM.Api.Modules.Authorization;

/// <summary>
/// Defensive policy for a FUTURE audited support view-as session. This is not
/// an impersonation token, endpoint permission or replacement for persisted
/// authorization/auditing. Callers must source flags from server-verified
/// permissions, institutional identities and resource policies.
/// </summary>
public static class SupportViewAsPolicy
{
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(30);

    public static SupportViewAsDecision EvaluateStart(
        SupportViewAsSession session,
        bool operatorHasVerifiedReadSupportPermission,
        bool targetIsPrivileged,
        DateTimeOffset nowUtc)
    {
        if (!operatorHasVerifiedReadSupportPermission)
            return Deny("SUPPORT_VIEW_PERMISSION_REQUIRED");
        if (string.IsNullOrWhiteSpace(session.OperatorSubject) ||
            string.IsNullOrWhiteSpace(session.TargetSubject))
            return Deny("SUPPORT_VIEW_IDENTITY_REQUIRED");
        if (string.Equals(session.OperatorSubject, session.TargetSubject, StringComparison.Ordinal))
            return Deny("SUPPORT_VIEW_SELF_DENIED");
        if (targetIsPrivileged)
            return Deny("SUPPORT_VIEW_PRIVILEGED_TARGET_DENIED");
        if (!HasValidReason(session.Reason))
            return Deny("SUPPORT_VIEW_REASON_REQUIRED");
        if (session.StartedAtUtc > nowUtc ||
            nowUtc >= session.ExpiresAtUtc ||
            session.ExpiresAtUtc <= session.StartedAtUtc ||
            session.ExpiresAtUtc - session.StartedAtUtc > MaxLifetime)
            return Deny("SUPPORT_VIEW_SESSION_EXPIRED_OR_INVALID");

        return Allow();
    }

    /// <summary>
    /// Even an active session cannot perform writes. The caller must verify
    /// target permissions and an explicit allowlist of read-only endpoints.
    /// Generic GETs (including sensitive resources or GET side effects) are
    /// NOT implicitly approved.
    /// </summary>
    public static SupportViewAsDecision EvaluateRead(
        SupportViewAsSession session,
        string? authenticatedOperatorSubject,
        string? httpMethod,
        bool operatorHasVerifiedReadSupportPermission,
        bool targetIsPrivileged,
        bool endpointExplicitlyAllowsSupportRead,
        bool targetCanReadResource,
        bool resourceContainsRestrictedPersonalData,
        DateTimeOffset nowUtc)
    {
        if (httpMethod is not ("GET" or "HEAD"))
            return Deny("SUPPORT_VIEW_READ_ONLY");
        if (!string.Equals(authenticatedOperatorSubject, session.OperatorSubject, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(authenticatedOperatorSubject))
            return Deny("SUPPORT_VIEW_OPERATOR_MISMATCH");

        var startDecision = EvaluateStart(
            session, operatorHasVerifiedReadSupportPermission, targetIsPrivileged, nowUtc);
        if (!startDecision.Allowed) return startDecision;

        if (!endpointExplicitlyAllowsSupportRead ||
            !targetCanReadResource ||
            resourceContainsRestrictedPersonalData)
            return Deny("SUPPORT_VIEW_RESOURCE_DENIED");

        return Allow();
    }

    private static bool HasValidReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return false;
        var value = reason.Trim();
        return value.Length is >= 10 and <= 500 && !value.Any(char.IsControl);
    }

    private static SupportViewAsDecision Allow() => new(true, "ALLOWED");
    private static SupportViewAsDecision Deny(string code) => new(false, code);
}

public sealed record SupportViewAsSession(
    string OperatorSubject,
    string TargetSubject,
    string Reason,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record SupportViewAsDecision(bool Allowed, string Code);
