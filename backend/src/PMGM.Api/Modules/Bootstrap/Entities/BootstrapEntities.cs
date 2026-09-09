namespace PMGM.Api.Modules.Bootstrap.Entities;

public sealed class InstitutionalBootstrapApplication
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string PackageKey { get; set; }
    public int PackageVersion { get; set; }
    public required string PayloadSha256 { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset AppliedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public required string SummaryJson { get; set; }
}

public sealed class SecurityProfileDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Scope { get; set; }
    public required string Category { get; set; }
    public required string Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class OfficeDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string OrganizationType { get; set; }
    public string? AliasesJson { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
