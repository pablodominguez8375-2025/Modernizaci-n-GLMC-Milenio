namespace PMGM.Api.Modules.RegimenInterior.Entities;

/// <summary>
/// Constancia técnica append-only de revisión GT. Ni este modelo ni un hash
/// implican certificación histórica de saldos o autorización electoral.
/// </summary>
public sealed class AssemblyTreasurySignedReviewRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AssemblyId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProposalEventId { get; set; }
    public Guid ReviewEventId { get; set; }
    public AssemblyTreasuryCertificationEventRecord? ProposalEvent { get; set; }
    public AssemblyTreasuryCertificationEventRecord? ReviewEvent { get; set; }
    public required string ReviewerSubject { get; set; }
    public required string TrustedKeyId { get; set; }
    public required string DocumentSha256Hex { get; set; }
    public required string SignatureBase64 { get; set; }
    public DateTimeOffset SignedAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
