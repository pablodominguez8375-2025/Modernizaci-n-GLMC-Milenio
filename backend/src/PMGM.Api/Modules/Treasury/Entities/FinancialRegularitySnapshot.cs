using PMGM.Api.Modules.Core.Entities;
using PMGM.Api.Modules.Membership.Entities;

namespace PMGM.Api.Modules.Treasury.Entities;

public sealed class FinancialRegularitySnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid? MemberId { get; set; }
    public Member? Member { get; set; }
    public required string Scope { get; set; }
    public required string Status { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string? SourceReference { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
