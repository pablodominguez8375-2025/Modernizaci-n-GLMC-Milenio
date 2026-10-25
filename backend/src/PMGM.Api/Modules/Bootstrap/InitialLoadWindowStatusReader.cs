using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Bootstrap.Entities;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Internal read-only status projection. This is not registered as an HTTP API
/// and cannot change the window or authorize importing people.
/// Untrusted/missing/corrupt database state fails closed as "unavailable".
/// </summary>
public static class InitialLoadWindowStatusReader
{
    public const string Unavailable = "unavailable";

    public sealed record Snapshot(
        string Status,
        long? Version,
        bool CanApply,
        int WritesPerformed,
        string Code);

    public static async Task<Snapshot> ReadAsync(
        BootstrapDbContext db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        try
        {
            var window = await db.InitialLoadWindows.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == InitialLoadWindow.SingletonId, cancellationToken);
            return Project(window);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is not OperationCanceledException
                                  and not OutOfMemoryException)
        {
            // Deliberately no exception text, connection string, SQL, or
            // stack trace in the result. No personal/juridical proof leaks.
            return Denied();
        }
    }

    public static Snapshot Project(InitialLoadWindow? window)
    {
        if (window is null || window.Id != InitialLoadWindow.SingletonId
            || window.Version < 0 || window.UpdatedAtUtc < window.CreatedAtUtc)
            return Denied();

        switch (window.Status)
        {
            case InitialLoadWindow.NotOpened
                when window.Version == 0
                     && window.LegalApprovalDigest is null
                     && window.OpenedAtUtc is null && window.OpenedAuditEventId is null
                     && window.ClosedAtUtc is null && window.ClosedAuditEventId is null:
                return new Snapshot(InitialLoadWindow.NotOpened, 0, false, 0, "OK");
            case InitialLoadWindow.Open
                when window.Version == 1
                     && DigestIsValid(window.LegalApprovalDigest)
                     && window.OpenedAtUtc.HasValue
                     && window.OpenedAuditEventId.HasValue
                     && window.ClosedAtUtc is null && window.ClosedAuditEventId is null:
                return new Snapshot(InitialLoadWindow.Open, 1, false, 0, "OK");
            case InitialLoadWindow.Closed
                when window.Version == 2
                     && DigestIsValid(window.LegalApprovalDigest)
                     && window.OpenedAtUtc.HasValue
                     && window.OpenedAuditEventId.HasValue
                     && window.ClosedAtUtc.HasValue && window.ClosedAuditEventId.HasValue
                     && window.ClosedAuditEventId != window.OpenedAuditEventId
                     && window.ClosedAtUtc >= window.OpenedAtUtc:
                return new Snapshot(InitialLoadWindow.Closed, 2, false, 0, "OK");
            default:
                return Denied();
        }
    }

    private static Snapshot Denied() =>
        new(Unavailable, null, false, 0, "WINDOW_STATUS_UNAVAILABLE");

    private static bool DigestIsValid(string? value)
    {
        if (value?.Length != 64) return false;
        foreach (var c in value)
            if (c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        return true;
    }
}
