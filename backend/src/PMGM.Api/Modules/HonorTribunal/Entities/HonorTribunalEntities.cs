namespace PMGM.Api.Modules.HonorTribunal.Entities;

public sealed class HonorCase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; set; }
    public required string CaseNumber { get; set; }
    public DateOnly OpenedOn { get; set; }
    public required string Status { get; set; }
    public required string Classification { get; set; }
    public bool Reserved { get; set; } = true;
    public string? Resolution { get; set; }
    public DateOnly? ResolvedOn { get; set; }
    public required string CreatedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class HonorSanction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid HonorCaseId { get; set; }
    public HonorCase HonorCase { get; set; } = null!;
    public Guid PersonId { get; set; }
    public required string Type { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool TotalLossOfRights { get; set; }
    public bool AffectsAttendance { get; set; }
    public bool AffectsVoting { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
    public required string RecordedBySubject { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
