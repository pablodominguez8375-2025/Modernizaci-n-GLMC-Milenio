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

    internal sealed record Inventory(
        int OrganizationRows, int WorkshopTypeRows,
        int MissingNumbers, int DuplicateNumbers,
        bool CanonicalCodeMappingVerified = false,
        bool CanApply = false, int WritesPerformed = 0,
        int MissingOrienteCodes = 0, int DuplicateCompositeKeys = 0);

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

    /// <summary>
    /// Conservative workshop candidate projection. A candidate is not an
    /// identity match: Number is deliberately NOT mapped to workshopCode.
    /// Reconciliation remains blocked until a canonical source is approved.
    /// </summary>
    internal static async Task<IReadOnlyList<Workshop>> ReadWorkshopCandidatesAsync(
        BootstrapDbContext db, CancellationToken cancellationToken)
    {
        return await db.Organizations.AsNoTracking()
            .Where(x => x.Type == "workshop")
            .Select(x => new Workshop(x.Id, x.Name, x.Number, x.Type,
                x.OrienteCode, x.City, x.Country))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Database-backed inventory for an internal preflight. These counts are
    /// diagnostic only: neither Type nor Number establishes the canonical
    /// workshopCode mapping. No personal data, identifiers or names escape.
    /// </summary>
    internal static async Task<Inventory> InspectInventoryAsync(
        BootstrapDbContext db, CancellationToken cancellationToken)
    {
        var rows = await ReadAsync(db, cancellationToken);
        var numbered = rows.Where(x => !string.IsNullOrWhiteSpace(x.Number))
            .GroupBy(x => x.Number!, StringComparer.Ordinal)
            .ToArray();
        return new Inventory(
            rows.Count,
            rows.Count(x => string.Equals(x.Type, "workshop", StringComparison.OrdinalIgnoreCase)),
            rows.Count(x => string.IsNullOrWhiteSpace(x.Number)),
            numbered.Count(x => x.Count() > 1),
            MissingOrienteCodes: rows.Count(x => x.Type == "workshop" &&
                string.IsNullOrWhiteSpace(x.OrienteCode)),
            DuplicateCompositeKeys: rows.Where(x => x.Type == "workshop" &&
                !string.IsNullOrWhiteSpace(x.Number) &&
                !string.IsNullOrWhiteSpace(x.OrienteCode))
                .GroupBy(x => (x.Number, x.OrienteCode))
                .Count(g => g.Count() > 1));
    }
}
