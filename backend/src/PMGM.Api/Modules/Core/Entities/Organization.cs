namespace PMGM.Api.Modules.Core.Entities;

public sealed class Organization
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Number { get; set; }
    public required string Type { get; set; }
    public Guid? ParentOrganizationId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
