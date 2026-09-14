using PMGM.Api.Modules.Core.Entities;

namespace PMGM.Api.Modules.Hospitalaria.Entities;

public sealed class HospitalariaRegularitySnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public required string Status { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
