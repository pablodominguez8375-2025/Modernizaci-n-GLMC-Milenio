namespace PMGM.Api.Modules.Core.Entities;

public sealed class Person
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FirstNames { get; set; }
    public required string LastNames { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
