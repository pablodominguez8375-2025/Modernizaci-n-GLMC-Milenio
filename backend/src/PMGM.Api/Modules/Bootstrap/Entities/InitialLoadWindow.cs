namespace PMGM.Api.Modules.Bootstrap.Entities;

/// <summary>
/// Singleton state for the initial enrollment window; contains only workflow
/// metadata, never individual member names, identity numbers or upload bytes.
/// Only a later privileged transactional service may request transitions.
/// Database CHECK constraints and triggers forbid reopening and deletion.
/// </summary>
public sealed class InitialLoadWindow
{
    public const short SingletonId = 1;
    public const string NotOpened = "not_opened";
    public const string Open = "open";
    public const string Closed = "closed";

    public short Id { get; set; } = SingletonId;
    public string Status { get; set; } = NotOpened;
    public long Version { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Digest of a verified non-PII approval reference, not approval by itself.
    public string? LegalApprovalDigest { get; set; }
    public DateTimeOffset? OpenedAtUtc { get; set; }
    public Guid? OpenedAuditEventId { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public Guid? ClosedAuditEventId { get; set; }
}
