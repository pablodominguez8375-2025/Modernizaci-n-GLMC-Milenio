using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap.Entities;

namespace PMGM.Api.Modules.Bootstrap;

/// <summary>
/// Server-owned evidence source; never implement from posted booleans or HTTP
/// headers. No implementation is registered in the application in this phase.
/// </summary>
public interface IInitialLoadTransitionEvidenceSource
{
    Task<InitialLoadTransitionEvidence?> ReadVerifiedAsync(
        InitialLoadAdmissionPolicy.Operation operation, CancellationToken cancellationToken);
}

public sealed record InitialLoadTransitionEvidence(
    InitialLoadAdmissionPolicy.Evidence Admission,
    string? VerifiedLegalDigest);

/// <summary>
/// Transactional writer for #694. NOT mapped as an HTTP endpoint or registered in
/// DI. A later phase must supply a server-side evidence source and legal signoff.
/// Does not read or write personal records and cannot import a CSV.
/// </summary>
public sealed class InitialLoadWindowAtomicTransition(BootstrapDbContext db)
{
    public sealed record Result(bool Applied, string Status, long? Version,
        int WritesPerformed, string Code)
    {
        public static Result Denied(string code) =>
            new(false, InitialLoadWindowStatusReader.Unavailable, null, 0, code);
    }

    /// <summary>
    /// Builds an immutable value snapshot from a previously validated plan.
    /// Rechecks state/version/invariants so fabricated or stale plans fail closed.
    /// Never edits the supplied entity and never performs database writes.
    /// </summary>
    public static InitialLoadWindow? Materialize(
        InitialLoadWindow? current,
        InitialLoadWindowTransitionGuard.PlanResult? plan,
        DateTimeOffset atUtc,
        Guid auditEventId,
        string? serverVerifiedLegalDigest)
    {
        if (current is null || plan is null || auditEventId == Guid.Empty ||
            atUtc.Offset != TimeSpan.Zero ||
            InitialLoadWindowStatusReader.Project(current).Status ==
                InitialLoadWindowStatusReader.Unavailable ||
            !plan.PreconditionsSatisfied || plan.CanApply || plan.WritesPerformed != 0 ||
            plan.Code != "TRANSITION_PREPARED" ||
            plan.ExpectedVersion != current.Version ||
            plan.ProposedVersion != current.Version + 1 ||
            atUtc < current.UpdatedAtUtc || atUtc < current.CreatedAtUtc)
            return null;

        var opening = current.Status == InitialLoadWindow.NotOpened &&
                      plan.ProposedStatus == InitialLoadWindow.Open &&
                      current.Version == 0;
        var closing = current.Status == InitialLoadWindow.Open &&
                      plan.ProposedStatus == InitialLoadWindow.Closed &&
                      current.Version == 1 &&
                      current.OpenedAtUtc.HasValue &&
                      atUtc >= current.OpenedAtUtc.Value &&
                      current.OpenedAuditEventId != auditEventId;
        if (!opening && !closing) return null;

        // Require an independently checked digest on opening; never interpret
        // the hash itself as legal authorization.
        if (opening && !ValidDigest(serverVerifiedLegalDigest)) return null;
        if (closing && serverVerifiedLegalDigest is not null) return null;

        var result = new InitialLoadWindow
        {
            Id = current.Id,
            Status = plan.ProposedStatus!,
            Version = plan.ProposedVersion.Value,
            CreatedAtUtc = current.CreatedAtUtc,
            UpdatedAtUtc = atUtc,
            LegalApprovalDigest = opening ? serverVerifiedLegalDigest : current.LegalApprovalDigest,
            OpenedAtUtc = opening ? atUtc : current.OpenedAtUtc,
            OpenedAuditEventId = opening ? auditEventId : current.OpenedAuditEventId,
            ClosedAtUtc = closing ? atUtc : null,
            ClosedAuditEventId = closing ? auditEventId : null
        };
        return InitialLoadWindowStatusReader.Project(result).Status ==
            InitialLoadWindowStatusReader.Unavailable ? null : result;
    }

    /// <summary>
    /// No route calls this method. JWT and master registration are verified here
    /// again even if a future HTTP adapter has its own authorization.
    /// </summary>
    public async Task<Result> ExecuteAsync(
        HttpContext context,
        IInstitutionalAccessService access,
        IConfiguration configuration,
        IInitialLoadTransitionEvidenceSource evidenceSource,
        InitialLoadAdmissionPolicy.Operation operation,
        CancellationToken cancellationToken)
    {
        if (operation is not (InitialLoadAdmissionPolicy.Operation.RequestOpen or
                             InitialLoadAdmissionPolicy.Operation.RequestClose))
            return Result.Denied("INVALID_TRANSITION");

        var auth = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        if (!auth.Succeeded || auth.Principal is null ||
            !InitialLoadWindowStatusEndpoints.IsRegisteredMaster(
                auth.Principal, access, configuration))
            return Result.Denied("MASTER_REQUIRED");

        // The provider is server-owned and currently has no runtime binding.
        var verified = await evidenceSource.ReadVerifiedAsync(operation, cancellationToken);
        if (verified is null) return Result.Denied("SERVER_EVIDENCE_UNAVAILABLE");

        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);
            // This is the same singleton row lock used by the legacy bootstrap.
            // ToListAsync avoids EF composing a subquery around FOR UPDATE.
            var windows = await db.InitialLoadWindows.FromSqlRaw("""
                SELECT * FROM core.initial_load_windows WHERE "Id" = 1 FOR UPDATE
                """).ToListAsync(cancellationToken);
            if (windows.Count != 1)
                return Result.Denied("WINDOW_STATUS_UNAVAILABLE");

            var window = windows[0];
            var snapshot = InitialLoadWindowStatusReader.Project(window);
            if (snapshot.Status == InitialLoadWindowStatusReader.Unavailable)
                return Result.Denied("WINDOW_STATUS_UNAVAILABLE");

            var before = snapshot.Status switch
            {
                InitialLoadWindow.NotOpened => InitialLoadAdmissionPolicy.Window.NotOpened,
                InitialLoadWindow.Open => InitialLoadAdmissionPolicy.Window.Open,
                InitialLoadWindow.Closed => InitialLoadAdmissionPolicy.Window.Closed,
                _ => throw new InvalidOperationException("Unexpected verified window status")
            };
            var admission = InitialLoadAdmissionPolicy.Evaluate(
                auth.Principal, access, before, operation, verified.Admission);
            var nowUtc = DateTimeOffset.UtcNow;
            var auditId = Guid.NewGuid();
            var plan = InitialLoadWindowTransitionGuard.Plan(
                window, operation, admission, nowUtc, auditId,
                operation == InitialLoadAdmissionPolicy.Operation.RequestOpen
                    ? verified.VerifiedLegalDigest : null);
            if (!plan.PreconditionsSatisfied)
                return Result.Denied(plan.Code);

            var next = Materialize(window, plan, nowUtc, auditId,
                operation == InitialLoadAdmissionPolicy.Operation.RequestOpen
                    ? verified.VerifiedLegalDigest : null);
            if (next is null) return Result.Denied("INVALID_WINDOW_TRANSITION");

            var actor = auth.Principal.FindFirstValue("sub") ??
                        auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(actor) || actor.Length > 320)
                return Result.Denied("MASTER_REQUIRED");

            var audit = new AuditEvent
            {
                Id = auditId,
                OccurredAtUtc = nowUtc,
                Action = next.Status == InitialLoadWindow.Open
                    ? "system.initial_load.window_open" : "system.initial_load.window_close",
                EntityType = "InitialLoadWindow",
                EntityId = "1",
                ActorSubject = actor,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                Menu = "Sistema",
                Submenu = "Carga inicial",
                Summary = "Cambio de estado de carga inicial",
                Result = "success",
                CorrelationId = string.IsNullOrWhiteSpace(context.TraceIdentifier)
                    ? "server-generated" : context.TraceIdentifier[..Math.Min(160, context.TraceIdentifier.Length)],
                MetadataJson = JsonSerializer.Serialize(new
                {
                    PreviousStatus = window.Status,
                    NewStatus = next.Status,
                    PreviousVersion = window.Version,
                    NewVersion = next.Version
                })
            };
            db.AuditEvents.Add(audit);
            // Insert audit first to satisfy the window's FK to audit_events.
            await db.SaveChangesAsync(cancellationToken);
            window.Status = next.Status;
            window.Version = next.Version;
            window.UpdatedAtUtc = next.UpdatedAtUtc;
            window.LegalApprovalDigest = next.LegalApprovalDigest;
            window.OpenedAtUtc = next.OpenedAtUtc;
            window.OpenedAuditEventId = next.OpenedAuditEventId;
            window.ClosedAtUtc = next.ClosedAtUtc;
            window.ClosedAuditEventId = next.ClosedAuditEventId;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new Result(true, next.Status, next.Version, 2, "TRANSITION_COMMITTED");
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Denied("WINDOW_CONFLICT");
        }
        catch (DbUpdateException)
        {
            return Result.Denied("WINDOW_WRITE_FAILED");
        }
        catch (PostgresException)
        {
            // No SQLSTATE, SQL, credentials or approval material in responses.
            return Result.Denied("WINDOW_WRITE_FAILED");
        }
    }

    private static bool ValidDigest(string? digest) =>
        digest?.Length == 64 && digest.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
