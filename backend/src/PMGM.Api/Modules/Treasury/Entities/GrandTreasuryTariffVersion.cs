namespace PMGM.Api.Modules.Treasury.Entities;

public sealed class GrandTreasuryTariffVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Version { get; set; }
    public required string Number { get; set; }
    public DateOnly DecreeDate { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveUntil { get; set; }
    public required string SourceReference { get; set; }
    public required string Status { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
