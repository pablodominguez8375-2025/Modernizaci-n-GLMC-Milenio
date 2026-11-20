using Microsoft.EntityFrameworkCore;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Read-only PostgreSQL snapshot for the workshop portion of #694.
/// Deliberately returns internal keys to the trusted server only: callers
/// MUST NOT serialize these values in HTTP responses, logs or audit metadata.
/// This is not a CSV-to-DB matcher and never performs any write.
/// </summary>
internal static class InitialLoadWorkshopSnapshotReader
{
    internal sealed record Workshop(
        Guid Id, string Name, string? Number, string Type,
        string? OrienteCode, string? City, string? Country);

    internal static async Task<IReadOnlyList<Workshop>> ReadAsync(
        BootstrapDbContext db, CancellationToken cancellationToken)
    {
        // Do not infer that Number is the CSV workshopCode: the canonical
        // mapping and uniqueness rules must be established before matching.
        return await db.Organizations.AsNoTracking()
            .Select(x => new Workshop(x.Id, x.Name, x.Number, x.Type,
                x.OrienteCode, x.City, x.Country))
            .ToListAsync(cancellationToken);
    }
}
