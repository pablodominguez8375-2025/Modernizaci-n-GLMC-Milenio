namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Read-only idempotency decision for future #694 import ledger. No data load,
/// DB write or legal judgment. A UNIQUE HMAC key and row lock will be required
/// at persistence time. Input must come from a server-verified preflight receipt.
/// </summary>
public static class InitialLoadBatchIdempotencyPolicy
{
    public enum Status { Pending = 0, Applied = 1, Reversed = 2 }

    public sealed record PersistedBatch(
        string Fingerprint, Status Status, long Version,
        Guid ReservedAuditEventId, Guid? AppliedAuditEventId = null,
        Guid? ReversedAuditEventId = null);

    public sealed record Decision(
        string Code, bool MayProposeReservation, bool AlreadyApplied,
        bool CanApply = false, int WritesPerformed = 0);

    public static Decision Inspect(
        string? verifiedBatchFingerprint, PersistedBatch? persisted)
    {
        static Decision Denied(string code) => new(code, false, false);

        if (!ValidFingerprint(verifiedBatchFingerprint))
            return Denied("INVALID_VERIFIED_FINGERPRINT");

        // The future database transaction MUST enforce uniqueness, absence
        // and concurrency. No persisted row is never permission to apply.
        if (persisted is null)
            return new Decision("RESERVATION_PROPOSABLE", true, false);

        if (!ValidPersisted(persisted))
            return Denied("BATCH_LEDGER_UNAVAILABLE");

        if (!string.Equals(verifiedBatchFingerprint, persisted.Fingerprint,
                StringComparison.Ordinal))
            return Denied("BATCH_FINGERPRINT_CONFLICT");

        return persisted.Status switch
        {
            Status.Pending => Denied("BATCH_IN_PROGRESS"),
            Status.Applied => new Decision("ALREADY_APPLIED", false, true),
            Status.Reversed => Denied("BATCH_ALREADY_REVERSED"),
            _ => Denied("BATCH_LEDGER_UNAVAILABLE")
        };
    }

    private static bool ValidPersisted(PersistedBatch record)
    {
        if (!ValidFingerprint(record.Fingerprint) ||
            record.ReservedAuditEventId == Guid.Empty ||
            !Enum.IsDefined(record.Status))
            return false;

        return record.Status switch
        {
            Status.Pending => record.Version == 0 &&
                record.AppliedAuditEventId is null &&
                record.ReversedAuditEventId is null,
            Status.Applied => record.Version == 1 &&
                ValidDistinct(record.AppliedAuditEventId,
                    record.ReservedAuditEventId) &&
                record.ReversedAuditEventId is null,
            Status.Reversed => record.Version == 2 &&
                ValidDistinct(record.AppliedAuditEventId,
                    record.ReservedAuditEventId) &&
                ValidDistinct(record.ReversedAuditEventId,
                    record.ReservedAuditEventId,
                    record.AppliedAuditEventId!.Value),
            _ => false
        };
    }

    private static bool ValidDistinct(Guid? auditId, params Guid[] existing) =>
        auditId.HasValue && auditId.Value != Guid.Empty &&
        !existing.Contains(auditId.Value);

    private static bool ValidFingerprint(string? fingerprint) =>
        fingerprint is { Length: 64 } &&
        fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
