using PMGM.Api.Modules.Bootstrap;
using Xunit;

namespace PMGM.Api.Tests;

public sealed class InitialLoadBatchIdempotencyPolicyTests
{
    [Fact]
    public void Invalid_fingerprint_does_not_allow_reservation()
    {
        var result = InitialLoadBatchIdempotencyPolicy.Inspect(null, null);
        Assert.False(result.MayProposeReservation);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Missing_ledger_can_only_propose_reservation()
    {
        var result = InitialLoadBatchIdempotencyPolicy.Inspect(new string('a', 64), null);
        Assert.Equal("RESERVATION_PROPOSABLE", result.Code);
        Assert.True(result.MayProposeReservation);
        Assert.False(result.CanApply);
        Assert.Equal(0, result.WritesPerformed);
    }

    [Fact]
    public void Pending_applied_and_reversed_never_allow_repeat()
    {
        var hmac = new string('a', 64);
        var reserved = Guid.NewGuid();
        var applied = Guid.NewGuid();
        var reversed = Guid.NewGuid();
        var pending = new InitialLoadBatchIdempotencyPolicy.PersistedBatch(
            hmac, InitialLoadBatchIdempotencyPolicy.Status.Pending, 0, reserved);
        var statusPending = InitialLoadBatchIdempotencyPolicy.Inspect(hmac, pending);
        Assert.Equal("BATCH_IN_PROGRESS", statusPending.Code);
        var done = pending with
        {
            Status = InitialLoadBatchIdempotencyPolicy.Status.Applied,
            Version = 1, AppliedAuditEventId = applied
        };
        var statusApplied = InitialLoadBatchIdempotencyPolicy.Inspect(hmac, done);
        Assert.Equal("ALREADY_APPLIED", statusApplied.Code);
        Assert.True(statusApplied.AlreadyApplied);
        var rolledBack = done with
        {
            Status = InitialLoadBatchIdempotencyPolicy.Status.Reversed,
            Version = 2, ReversedAuditEventId = reversed
        };
        var statusReversed = InitialLoadBatchIdempotencyPolicy.Inspect(hmac, rolledBack);
        Assert.Equal("BATCH_ALREADY_REVERSED", statusReversed.Code);
        foreach (var response in new[] { statusPending, statusApplied, statusReversed })
        {
            Assert.False(response.CanApply);
            Assert.False(response.MayProposeReservation);
            Assert.Equal(0, response.WritesPerformed);
        }
    }

    [Fact]
    public void Mismatched_or_noncanonical_fingerprint_is_rejected()
    {
        var fingerprint = new string('a', 64);
        var record = new InitialLoadBatchIdempotencyPolicy.PersistedBatch(
            new string('b', 64), InitialLoadBatchIdempotencyPolicy.Status.Pending,
            0, Guid.NewGuid());
        Assert.Equal("BATCH_FINGERPRINT_CONFLICT",
            InitialLoadBatchIdempotencyPolicy.Inspect(fingerprint, record).Code);
        Assert.Equal("INVALID_VERIFIED_FINGERPRINT",
            InitialLoadBatchIdempotencyPolicy.Inspect(new string('A', 64), null).Code);
        Assert.Equal("INVALID_VERIFIED_FINGERPRINT",
            InitialLoadBatchIdempotencyPolicy.Inspect("invalid", null).Code);
    }

    [Fact]
    public void Corrupt_version_and_audit_ids_fail_closed()
    {
        var fingerprint = new string('a', 64);
        var reserved = Guid.NewGuid();
        var applied = Guid.NewGuid();
        var pending = new InitialLoadBatchIdempotencyPolicy.PersistedBatch(
            fingerprint, InitialLoadBatchIdempotencyPolicy.Status.Pending, 0, reserved);
        var broken = new[]
        {
            pending with { Version = 1 },
            pending with { ReservedAuditEventId = Guid.Empty },
            pending with { AppliedAuditEventId = applied },
            pending with { Status = (InitialLoadBatchIdempotencyPolicy.Status)99 },
            pending with { Status = InitialLoadBatchIdempotencyPolicy.Status.Applied,
                Version = 1, AppliedAuditEventId = reserved },
            pending with { Status = InitialLoadBatchIdempotencyPolicy.Status.Reversed,
                Version = 2, AppliedAuditEventId = applied,
                ReversedAuditEventId = applied }
        };
        foreach (var row in broken)
        {
            var result = InitialLoadBatchIdempotencyPolicy.Inspect(fingerprint, row);
            Assert.Equal("BATCH_LEDGER_UNAVAILABLE", result.Code);
            Assert.False(result.CanApply);
            Assert.Equal(0, result.WritesPerformed);
        }
    }
}
