namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Immutable supplementary fields captured against one institutional candidate
/// dossier and the exact published form schema. Canonical identity, RUT, names,
/// contact details and workflow state remain in existing domain entities.
/// </summary>
public sealed class CandidateFormCapture
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CeremonyRequestId { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid SchemaVersionId { get; init; }
    public int SchemaVersion { get; init; }
    public required string ScopeKey { get; init; }
    public int CaptureRevision { get; init; }
    public required string ValuesJson { get; init; }
    public required string PayloadSha256 { get; init; }
    public required string CapturedBySubject { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
