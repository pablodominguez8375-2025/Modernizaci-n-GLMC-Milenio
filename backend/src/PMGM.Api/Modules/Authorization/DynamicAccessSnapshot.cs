namespace PMGM.Api.Modules.Authorization;

public sealed class DynamicAccessSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Version { get; set; }
    public string Payload { get; set; } = "{}";
    public DateTimeOffset RecordedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
