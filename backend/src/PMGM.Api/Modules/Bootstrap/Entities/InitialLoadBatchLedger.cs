namespace PMGM.Api.Modules.Bootstrap.Entities;

/// <summary>
/// Internal #694 batch ledger: HMAC identifiers and audit metadata ONLY.
/// Neither CSV bytes nor member identities are stored in this table.
/// A later authenticated transactional service may manage transitions.
/// </summary>
public sealed class InitialLoadBatchLedger
{
    public const string Pending = "pending";
    public const string Applied = "applied";
    public const string Reversed = "reversed";

    public string Fingerprint { get; set; } = string.Empty;
    public string Status { get; set; } = Pending;
    public long Version { get; set; }
    public Guid ReservedAuditEventId { get; set; }
    public Guid? AppliedAuditEventId { get; set; }
    public Guid? ReversedAuditEventId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? AppliedAtUtc { get; set; }
    public DateTimeOffset? ReversedAtUtc { get; set; }
}
