namespace PMGM.Api.Modules.FormConfiguration;

/// <summary>
/// Append-only, scoped definition of a published form. Values submitted by members
/// belong in their domain records and must reference the exact schema version.
/// Neither the definition nor its checksum may be edited after publication.
/// </summary>
public sealed class FormSchemaVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string ScopeKey { get; init; }
    public required string FormCode { get; init; }
    public int Version { get; init; }
    public DateTimeOffset EffectiveFromUtc { get; init; }
    public required string SchemaJson { get; init; }
    public required string PayloadSha256 { get; init; }
    public required string RecordedBySubject { get; init; }
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public static class FormSchemaScope
{
    public const string Order = "order";

    /// <summary>
    /// Scope comes from a trusted server-side institutional decision, never from
    /// a client-supplied scope string. Workshop existence is checked by the store.
    /// </summary>
    public static string ForOrganization(Guid? organizationId) => organizationId switch
    {
        null => Order,
        { } id when id != Guid.Empty => "lodge:" + id.ToString("N"),
        _ => throw new ArgumentException("Se requiere un Taller válido.", nameof(organizationId))
    };
}
