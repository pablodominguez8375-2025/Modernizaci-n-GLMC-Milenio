using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// Persistencia interna del conjunto acreditado C22. NO es un endpoint:
/// la fuente de certificaciones debe ser resuelta por infraestructura institucional
/// confiable antes de invocarla; no aceptar un lote enviado por HTTP.
/// Ninguna fila de esta función autoriza sufragio, asistencia ni cierre.
/// </summary>
public static class AssemblyAccreditedRosterPersistenceService
{
    public const string PendingReason = "OFFICIAL_ELIGIBILITY_EVIDENCE_PENDING";

    public static async Task<AssemblyAccreditedRosterPersistenceResult> PersistAsync(
        Guid assemblyId,
        DateOnly certifiedCutoff,
        AssemblyAccreditedMemberMaterializationResult prepared,
        HttpContext context,
        RegimenInteriorDbContext db,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(db);

        if (context.User.Identity?.IsAuthenticated != true ||
            !context.User.HasClaim(InstitutionalClaims.Scope, "order") ||
            !context.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior) ||
            string.IsNullOrWhiteSpace(context.User.FindFirst("sub")?.Value))
            return Rejected("REGIMEN_INTERIOR_AUTHORIZATION_REQUIRED");

        if (assemblyId == Guid.Empty || certifiedCutoff == default ||
            !IsTrustedShape(assemblyId, prepared))
            return Rejected("INVALID_ACCREDITED_MEMBER_BATCH");

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);

        // Bloqueo físico del registro raíz: serializa importaciones del mismo
        // corte y evita carrera con un cambio de estado de la Asamblea.
        var matches = await db.Assemblies.AsNoTracking().FromSqlInterpolated(
            $"SELECT * FROM core.assemblies WHERE \"Id\" = {assemblyId} FOR UPDATE")
            .ToArrayAsync(ct);
        var assembly = matches.SingleOrDefault();
        if (assembly is null)
            return Rejected("ASSEMBLY_NOT_FOUND");

        if (assembly.Status != "draft" ||
            assembly.CutoffDate != certifiedCutoff ||
            assembly.CutoffDate > assembly.MeetingDate ||
            assembly.ClosedAtUtc is not null ||
            assembly.ClosedBySubject is not null ||
            assembly.ClosureEvidenceDigest is not null)
            return Rejected("ASSEMBLY_STATE_OR_CUTOFF_CHANGED");

        // No reemplazar ni fusionar silenciosamente padrón: las evidencias
        // existentes requieren un procedimiento explícito de rectificación.
        if (await db.AssemblyMemberStatuses.AsNoTracking()
                .AnyAsync(x => x.AssemblyId == assemblyId, ct) ||
            await db.AssemblyEligibilities.AsNoTracking()
                .AnyAsync(x => x.AssemblyId == assemblyId, ct))
            return Rejected("ASSEMBLY_ROSTER_ALREADY_MATERIALIZED");

        var actor = context.User.FindFirst("sub")!.Value;
        foreach (var candidate in prepared.Members)
        {
            db.AssemblyMemberStatuses.Add(candidate);
            db.AssemblyEligibilities.Add(new AssemblyEligibilityRecord
            {
                AssemblyId = assemblyId,
                MemberStatusId = candidate.Id,
                Status = AssemblyVoterEligibilityPolicy.Pending,
                MayAttend = false,
                MayVote = false,
                ReasonCode = PendingReason,
                EvaluatedBySubject = actor
            });
        }
        await db.SaveChangesAsync(ct);

        var audit = AuditEventFactory.Create(context,
            "regimen.asamblea.accredited_candidates.materialized",
            nameof(AssemblyRecord), assemblyId.ToString("D"), null,
            AuditResults.Success,
            new { Count = prepared.Members.Count, State = "pending",
                VoteGranted = false, AttendanceGranted = false });
        // Auditoría en la MISMA transacción física, no en otro DbContext.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO core.audit_events
              ("Id", "OccurredAtUtc", "Action", "EntityType", "EntityId",
               "OrganizationId", "ActorSubject", "ActorDisplayName", "IpAddress",
               "Menu", "Submenu", "Summary", "Result", "CorrelationId", "MetadataJson")
            VALUES
              ({audit.Id}, {audit.OccurredAtUtc}, {audit.Action}, {audit.EntityType},
               {audit.EntityId}, {audit.OrganizationId}, {audit.ActorSubject},
               {audit.ActorDisplayName}, {audit.IpAddress}, {audit.Menu},
               {audit.Submenu}, {audit.Summary}, {audit.Result},
               {audit.CorrelationId}, CAST({audit.MetadataJson} AS jsonb))
            """, ct);

        await transaction.CommitAsync(ct);
        return new(true, prepared.Members.Count, null);
    }

    private static AssemblyAccreditedRosterPersistenceResult Rejected(string code) =>
        new(false, 0, code);

    private static bool IsTrustedShape(
        Guid assemblyId, AssemblyAccreditedMemberMaterializationResult prepared)
    {
        if (!prepared.Prepared || prepared.BlockingReasons.Count != 0 ||
            prepared.Members.Count is 0 or > AssemblyAccreditedMemberMaterializationPolicy.MaxSourceCandidates)
            return false;

        var officeIds = new HashSet<Guid>();
        var personIds = new HashSet<Guid>();
        var memberIds = new HashSet<Guid>();
        foreach (var row in prepared.Members)
        {
            if (row is null || row.AssemblyId != assemblyId ||
                row.Id == Guid.Empty || row.PersonId == Guid.Empty ||
                row.MemberId == Guid.Empty || row.OrganizationId == Guid.Empty ||
                row.SourceOfficeAssignmentId == Guid.Empty ||
                row.VerifiedAtUtc == default ||
                row.VerifiedAtUtc > DateTimeOffset.UtcNow.AddMinutes(5) ||
                row.EligibilityHistory.Count != 0 ||
                !personIds.Add(row.PersonId) || !memberIds.Add(row.MemberId) ||
                !officeIds.Add(row.SourceOfficeAssignmentId) ||
                !Bounded(row.SourceEvidenceReference, 500) ||
                !Bounded(row.FirstNamesAtCutoff, 240) ||
                !Bounded(row.LastNamesAtCutoff, 240) ||
                !Bounded(row.RutAtCutoff, 32) ||
                !Bounded(row.WorkshopAtCutoff, 240) ||
                row.Category is not (
                    AssemblyMemberCategoryPolicy.Representative or
                    AssemblyMemberCategoryPolicy.PermanentAssemblyMember) ||
                row.FullTermVerified !=
                    (row.Category == AssemblyMemberCategoryPolicy.PermanentAssemblyMember))
                return false;
        }
        return true;
    }

    private static bool Bounded(string? input, int maxLength) =>
        !string.IsNullOrWhiteSpace(input) && input.Trim().Length <= maxLength;
}

public sealed record AssemblyAccreditedRosterPersistenceResult(
    bool Persisted, int MemberCount, string? BlockingReason);
