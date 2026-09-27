namespace PMGM.Api.Modules.Core.Entities;

public sealed class Organization
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Number { get; set; }
    public required string Type { get; set; }
    public Guid? ParentOrganizationId { get; set; }
    public string? TreasuryTerritory { get; set; }
    public DateOnly? EstablishedOn { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? LogoObjectKey { get; set; }
    public string? LogoContentType { get; set; }
    public string? LogoSha256 { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
