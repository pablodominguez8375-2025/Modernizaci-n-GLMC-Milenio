using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Membership;
using PMGM.Api.Modules.Membership.Entities;
using PMGM.Api.Modules.RegimenInterior.Entities;

namespace PMGM.Api.Modules.RegimenInterior;

/// <summary>
/// #533 entrega 3 — acreditar asambleístas, informar saldos de Gran Tesorería, evaluar y cerrar el padrón.
/// Régimen Interior (alcance Orden); los saldos/pagos solo los valida Gran Tesorería o Gran Hospitalaria. Todo con evidencia ya registrada (constancias con acta, oficio de Gran
/// Tesorería); fail-closed; cierre en transacción serializable con resumen SHA-256, auditoría y guardia en la base.
/// </summary>
public static class AssemblyClosureEndpoints
{
    private static bool IsRegimen(HttpContext c) =>
        c.User.Identity?.IsAuthenticated == true &&
        c.User.HasClaim(InstitutionalClaims.Scope, "order") &&
        c.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.RegimenInterior) &&
        !string.IsNullOrWhiteSpace(c.User.FindFirst("sub")?.Value);

    // Los pagos y saldos solo los valida su responsable: Gran Tesorero o Gran Hospitalario (no Régimen Interior).
    private static bool IsPaymentValidator(HttpContext c) =>
        c.User.Identity?.IsAuthenticated == true &&
        c.User.HasClaim(InstitutionalClaims.Scope, "order") &&
        (c.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.GranTesoreria) ||
         c.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.GranHospitalaria)) &&
        !string.IsNullOrWhiteSpace(c.User.FindFirst("sub")?.Value);

    // Lectura para quien valida los pagos: asambleas en borrador, Talleres con asambleístas acreditados y saldo vigente.
    public static async Task<IResult> GetBalancesAsync(HttpContext context, RegimenInteriorDbContext db, PmgmDbContext appDb, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!IsPaymentValidator(context) && !IsRegimen(context)) return Results.Forbid();
        var assemblies = await db.Assemblies.AsNoTracking().Where(a => a.Status == "draft" && a.ClosedAtUtc == null && db.AssemblyMemberStatuses.Any(m => m.AssemblyId == a.Id))
            .OrderByDescending(a => a.CreatedAtUtc).Take(20).ToArrayAsync(ct);
        var ids = assemblies.Select(a => a.Id).ToArray();
        var pairs = await db.AssemblyMemberStatuses.AsNoTracking().Where(m => ids.Contains(m.AssemblyId))
            .Select(m => new { m.AssemblyId, m.OrganizationId, m.WorkshopAtCutoff }).Distinct().ToArrayAsync(ct);
        var balances = await db.AssemblyWorkshopBalanceEvents.AsNoTracking().Where(b => ids.Contains(b.AssemblyId)).ToArrayAsync(ct);
        var superseded = balances.Where(b => b.SupersedesEventId is not null).Select(b => b.SupersedesEventId!.Value).ToHashSet();
        var mySource = context.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.GranTesoreria) ? AssemblyClosureEngine.SourceTreasury
            : context.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.GranHospitalaria) ? AssemblyClosureEngine.SourceHospitalaria : null;
        return Results.Ok(assemblies.Select(a => new
        {
            id = a.Id, name = a.Name, cutoffDate = a.CutoffDate, meetingDate = a.MeetingDate, source = mySource,
            workshops = pairs.Where(p => p.AssemblyId == a.Id).OrderBy(p => p.WorkshopAtCutoff).Select(p =>
            {
                var cur = balances.Where(x => x.AssemblyId == a.Id && x.OrganizationId == p.OrganizationId && x.Source == mySource && !superseded.Contains(x.Id))
                    .OrderByDescending(x => x.OccurredAtUtc).FirstOrDefault();
                return new { organizationId = p.OrganizationId, name = p.WorkshopAtCutoff, amountPending = cur?.AmountPending, currency = cur?.Currency, sourceReference = cur?.SourceReference };
            }).ToArray()
        }));
    }

    private static async Task AuditAsync(HttpContext context, RegimenInteriorDbContext db, string action, Guid assemblyId, object meta, CancellationToken ct)
    {
        var audit = AuditEventFactory.Create(context, action, nameof(AssemblyRecord), assemblyId.ToString("D"), null, AuditResults.Success, meta);
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
    }

    private static async Task<AssemblyClosureEngine.Evidence> LoadEvidenceAsync(
        Guid assemblyId, IReadOnlyCollection<AssemblyMemberStatusRecord> members, DateOnly cutoff,
        RegimenInteriorDbContext db, PmgmDbContext appDb, CancellationToken ct)
    {
        var attestations = AssemblyAttestationPolicy.Current(
            await db.AssemblyAttestationEvents.AsNoTracking().Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct));
        var balanceEvents = await db.AssemblyWorkshopBalanceEvents.AsNoTracking().Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);
        var balances = AssemblyClosureEngine.CurrentBalances(balanceEvents);
        var personIds = members.Select(m => m.PersonId).ToArray();
        var restrictions = await db.InstitutionalRestrictions.AsNoTracking()
            .Where(x => personIds.Contains(x.PersonId) && x.RevokedAtUtc == null).ToArrayAsync(ct);
        var debt = await db.AssemblyDebtDecisionEvents.AsNoTracking().Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct);

        var memberIds = members.Select(m => m.MemberId).ToArray();
        var memberships = await appDb.Memberships.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && (x.StartDate == null || x.StartDate <= cutoff) && (x.EndDate == null || x.EndDate >= cutoff))
            .ToArrayAsync(ct);
        var statuses = await appDb.InstitutionalStatusEvents.AsNoTracking()
            .Where(x => memberIds.Contains(x.MemberId) && x.EffectiveDate <= cutoff).ToArrayAsync(ct);
        var active = new Dictionary<Guid, bool>();
        foreach (var id in memberIds)
            active[id] = IsActive(memberships.Where(x => x.MemberId == id).ToArray(), statuses.Where(x => x.MemberId == id).ToArray());
        return new AssemblyClosureEngine.Evidence(attestations, balances, restrictions, debt, active);
    }

    private static bool IsActive(IReadOnlyCollection<Membership.Entities.Membership> memberships, IReadOnlyCollection<InstitutionalStatusEvent> events)
    {
        if (memberships.Count != 1 || memberships.Single().Status != MembershipCodes.MembershipStatus.Active) return false;
        var latest = events.OrderByDescending(e => e.EffectiveDate).ThenByDescending(e => e.RecordedAtUtc).ThenByDescending(e => e.Id).FirstOrDefault();
        return latest is null || latest.EventType is MembershipCodes.InstitutionalStatus.Active or MembershipCodes.InstitutionalStatus.Reinstated;
    }

    // ---- Acreditar asambleístas -------------------------------------------------------------------------------
    public static async Task<IResult> AccreditAsync(Guid assemblyId, HttpContext context, RegimenInteriorDbContext db, PmgmDbContext appDb, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!IsRegimen(context)) return Results.Forbid();
        var assembly = await db.Assemblies.AsNoTracking().SingleOrDefaultAsync(a => a.Id == assemblyId, ct);
        if (assembly is null) return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });
        var attestations = AssemblyAttestationPolicy.Current(
            await db.AssemblyAttestationEvents.AsNoTracking().Where(x => x.AssemblyId == assemblyId).ToArrayAsync(ct));
        var missing = new[] { AssemblyAttestationPolicy.MasterPeriods, AssemblyAttestationPolicy.Membership }
            .Where(k => !attestations.ContainsKey(k)).ToArray();
        if (missing.Length > 0) return Results.Conflict(new { code = "ATTESTATIONS_MISSING", missing });

        AssemblyMemberSourceInventoryResult? inventory = null; string? officeCode = null;
        foreach (var code in AssemblyClosureReadinessEndpoints.MasterOfficeCodes)
        {
            var attempt = await AssemblyMemberSourceInventory.DiscoverAsync(appDb, assembly.CutoffDate, code, ct);
            if (attempt.BlockingReasons.Contains("MASTER_OFFICE_RECORDS_MISSING")) { inventory ??= attempt; continue; }
            inventory = attempt; officeCode = code; break;
        }
        if (inventory is null || officeCode is null || inventory.BlockingReasons.Count > 0)
            return Results.Conflict(new { code = "SOURCE_INVENTORY_BLOCKED", reasons = inventory?.BlockingReasons ?? ["MASTER_OFFICE_RECORDS_MISSING"] });

        var now = DateTimeOffset.UtcNow;
        var memberIds = inventory.CandidateFacts.Select(c => c.MemberId).ToArray();
        var offices = await appDb.OfficeAssignments.AsNoTracking()
            .Where(o => memberIds.Contains(o.MemberId) && o.OfficeType == officeCode).ToArrayAsync(ct);
        var masterActa = attestations[AssemblyAttestationPolicy.MasterPeriods];
        var membershipActa = attestations[AssemblyAttestationPolicy.Membership];
        var cutoff = assembly.CutoffDate;

        var identities = new List<AssemblyVerifiedMemberIdentity>();
        foreach (var c in inventory.CandidateFacts)
        {
            var mine = offices.Where(o => o.MemberId == c.MemberId).ToArray();
            var term = AssemblyMasterTermEvidenceAdapter.Classify(c.MemberId, cutoff, now, officeCode, true, mine,
                mine.Where(o => o.StartDate is not null && o.StartDate <= cutoff && !string.IsNullOrWhiteSpace(o.EvidenceReference)).Select(o =>
                    new AssemblyMasterTermAttestation(o.Id, o.MemberId, o.OrganizationId, o.StartDate!.Value, o.EndDate, cutoff,
                        o.EvidenceReference!, masterActa.ActaReference, true,
                        o.EndDate is not null && o.EndDate <= cutoff, o.EndDate is null || o.EndDate >= cutoff, masterActa.OccurredAtUtc)).ToArray());
            var active = c.CurrentRecordedMembershipStatus == MembershipCodes.MembershipStatus.Active &&
                (c.LatestRecordedInstitutionalStatus is null or MembershipCodes.InstitutionalStatus.Active or MembershipCodes.InstitutionalStatus.Reinstated);
            identities.Add(new AssemblyVerifiedMemberIdentity(c.PersonId == Guid.Empty ? Guid.Empty : c.PersonId, c.MemberId, c.MembershipWorkshopId,
                cutoff, cutoff, cutoff, true, true, active, membershipActa.ActaReference, membershipActa.ActaReference,
                c.FirstNames, c.LastNames, c.Rut ?? "", c.WorkshopName, term));
        }

        var prepared = AssemblyAccreditedMemberMaterializationPolicy.Prepare(assembly, identities, true, now);
        if (!prepared.Prepared) return Results.Conflict(new { code = "ACCREDITATION_BLOCKED", reasons = prepared.BlockingReasons });
        var result = await AssemblyAccreditedRosterPersistenceService.PersistAsync(assemblyId, cutoff, prepared, context, db, ct);
        return result.Persisted
            ? Results.Ok(new { assemblyId, accredited = result.MemberCount, provisional = true, mayVoteConfirmed = false })
            : Results.Conflict(new { code = result.BlockingReason });
    }

    // ---- Saldos informados por Gran Tesorería ------------------------------------------------------------------
    public static async Task<IResult> PostBalancesAsync(Guid assemblyId, AssemblyBalancesRequest request, HttpContext context, RegimenInteriorDbContext db, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!IsPaymentValidator(context)) return Results.Forbid();
        var reference = request?.SourceReference?.Trim(); var currency = request?.Currency?.Trim().ToUpperInvariant();
        if (request?.Items is null || request.Items.Count is 0 or > 5000 || string.IsNullOrWhiteSpace(reference) || reference.Length > 500 ||
            currency is null || currency.Length != 3 || currency.Any(ch => ch is < 'A' or > 'Z') ||
            request.Items.Any(i => i.OrganizationId == Guid.Empty || i.AmountPending < 0m || i.AmountPending > 1_000_000_000_000m ||
                decimal.Round(i.AmountPending, 2) != i.AmountPending) ||
            request.Items.GroupBy(i => i.OrganizationId).Any(g => g.Count() > 1))
            return Results.BadRequest(new { code = "INVALID_BALANCES" });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var assembly = await db.Assemblies.AsNoTracking().SingleOrDefaultAsync(a => a.Id == assemblyId, ct);
        if (assembly is null) return Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" });
        if (assembly.Status != "draft" || assembly.ClosedAtUtc is not null) return Results.Conflict(new { code = "ASSEMBLY_NOT_EDITABLE" });
        var workshops = (await db.AssemblyMemberStatuses.AsNoTracking().Where(m => m.AssemblyId == assemblyId).Select(m => m.OrganizationId).Distinct().ToArrayAsync(ct)).ToHashSet();
        if (workshops.Count == 0) return Results.Conflict(new { code = "NO_ACCREDITED_MEMBERS" });
        if (request.Items.Any(i => !workshops.Contains(i.OrganizationId))) return Results.BadRequest(new { code = "WORKSHOP_NOT_IN_ASSEMBLY" });

        var existing = await db.AssemblyWorkshopBalanceEvents.AsNoTracking().Where(b => b.AssemblyId == assemblyId).ToArrayAsync(ct);
        var superseded = existing.Where(b => b.SupersedesEventId is not null).Select(b => b.SupersedesEventId!.Value).ToHashSet();
        var actor = context.User.FindFirst("sub")!.Value;
        var source = context.User.HasClaim(InstitutionalClaims.Role, InstitutionalRoles.GranTesoreria)
            ? AssemblyClosureEngine.SourceTreasury : AssemblyClosureEngine.SourceHospitalaria;
        foreach (var item in request.Items)
        {
            var current = existing.Where(b => b.OrganizationId == item.OrganizationId && b.Source == source && !superseded.Contains(b.Id))
                .OrderByDescending(b => b.OccurredAtUtc).FirstOrDefault();
            db.AssemblyWorkshopBalanceEvents.Add(new AssemblyWorkshopBalanceEventRecord
            {
                AssemblyId = assemblyId, OrganizationId = item.OrganizationId, Source = source, AmountPending = item.AmountPending, Currency = currency,
                SourceReference = reference, RecordedBySubject = actor, CutoffDate = assembly.CutoffDate, SupersedesEventId = current?.Id
            });
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState is "23505" or "23514" or "40001")
        { return Results.Conflict(new { code = "ASSEMBLY_CHANGED_RETRY_REVIEW" }); }
        await AuditAsync(context, db, "regimen.asamblea.saldos_gran_tesoreria.registrados", assemblyId,
            new { Workshops = request.Items.Count, Currency = currency, ValidatedByRole = source }, ct);
        await tx.CommitAsync(ct);
        return Results.Ok(new { assemblyId, recorded = request.Items.Count, provisional = true, mayVoteConfirmed = false });
    }

    // ---- Evaluar ------------------------------------------------------------------------------------------------
    public static async Task<IResult> EvaluateAsync(Guid assemblyId, HttpContext context, RegimenInteriorDbContext db, PmgmDbContext appDb, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!IsRegimen(context)) return Results.Forbid();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var outcome = await EvaluateInTransactionAsync(assemblyId, context, db, appDb, ct);
        if (outcome.Error is not null) return outcome.Error;
        await AuditAsync(context, db, "regimen.asamblea.evaluada", assemblyId,
            new { Appended = outcome.Appended, outcome.Counts, VoteGranted = false }, ct);
        try { await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState is "23505" or "23514" or "40001")
        { return Results.Conflict(new { code = "ASSEMBLY_CHANGED_RETRY_REVIEW" }); }
        return Results.Ok(new { assemblyId, evaluated = outcome.Members!.Count, appended = outcome.Appended, counts = outcome.Counts, provisional = true, officiallyClosed = false });
    }

    private sealed record EvalOutcome(IResult? Error, AssemblyRecord? Assembly, IReadOnlyList<AssemblyMemberStatusRecord>? Members,
        IReadOnlyDictionary<Guid, AssemblyEligibilityRecord>? Latest, AssemblyClosureEngine.Evidence? Evidence, int Appended, IReadOnlyDictionary<string, int>? Counts);

    private static async Task<EvalOutcome> EvaluateInTransactionAsync(Guid assemblyId, HttpContext context, RegimenInteriorDbContext db, PmgmDbContext appDb, CancellationToken ct)
    {
        var assembly = (await db.Assemblies.FromSqlInterpolated($"SELECT * FROM core.assemblies WHERE \"Id\" = {assemblyId} FOR UPDATE").ToArrayAsync(ct)).SingleOrDefault();
        if (assembly is null) return new(Results.NotFound(new { code = "ASSEMBLY_NOT_FOUND" }), null, null, null, null, 0, null);
        if (assembly.Status != "draft" || assembly.ClosedAtUtc is not null)
            return new(Results.Conflict(new { code = "ASSEMBLY_NOT_EDITABLE" }), null, null, null, null, 0, null);
        var members = await db.AssemblyMemberStatuses.Where(m => m.AssemblyId == assemblyId).OrderBy(m => m.Id).Take(10001).ToArrayAsync(ct);
        if (members.Length == 0) return new(Results.Conflict(new { code = "NO_ACCREDITED_MEMBERS" }), null, null, null, null, 0, null);
        if (members.Length > 10000) return new(Results.Conflict(new { code = "ASSEMBLY_TOO_LARGE" }), null, null, null, null, 0, null);
        var evidence = await LoadEvidenceAsync(assemblyId, members, assembly.CutoffDate, db, appDb, ct);
        var missing = AssemblyClosureEngine.MissingAttestations(evidence.Attestations);
        if (missing.Length > 0) return new(Results.Conflict(new { code = "ATTESTATIONS_MISSING", missing }), null, null, null, null, 0, null);

        var history = await db.AssemblyEligibilities.AsNoTracking().Where(e => e.AssemblyId == assemblyId).ToArrayAsync(ct);
        var latest = history.GroupBy(h => h.MemberStatusId).ToDictionary(g => g.Key, g =>
        {
            var superseded = g.Where(x => x.SupersedesEligibilityId is not null).Select(x => x.SupersedesEligibilityId!.Value).ToHashSet();
            return g.Where(x => !superseded.Contains(x.Id)).OrderByDescending(x => x.EvaluatedAtUtc).First();
        });
        var actor = context.User.FindFirst("sub")!.Value;
        var appended = 0;
        foreach (var m in members)
        {
            var next = AssemblyClosureEngine.NextRecord(assembly, m, latest.GetValueOrDefault(m.Id), evidence, actor);
            if (next is null) continue;
            db.AssemblyEligibilities.Add(next); latest[m.Id] = next; appended++;
        }
        if (appended > 0)
        {
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState is "23505" or "23514" or "40001")
            { return new(Results.Conflict(new { code = "ASSEMBLY_CHANGED_RETRY_REVIEW" }), null, null, null, null, 0, null); }
        }
        var counts = members.GroupBy(m => latest[m.Id].Status).ToDictionary(g => g.Key, g => g.Count());
        return new(null, assembly, members, latest, evidence, appended, counts);
    }

    // ---- Cerrar -------------------------------------------------------------------------------------------------
    public static async Task<IResult> CloseAsync(Guid assemblyId, AssemblyCloseRequest request, HttpContext context, RegimenInteriorDbContext db, PmgmDbContext appDb, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (!IsRegimen(context)) return Results.Forbid();
        var reference = request?.RosterReference?.Trim();
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 500) return Results.BadRequest(new { code = "ROSTER_REFERENCE_REQUIRED" });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var outcome = await EvaluateInTransactionAsync(assemblyId, context, db, appDb, ct);
        if (outcome.Error is not null) return outcome.Error;

        var now = DateTimeOffset.UtcNow;
        var assembly = outcome.Assembly!;
        var preflight = AssemblyClosureEngine.Preflight(assembly, outcome.Members!, outcome.Latest!, outcome.Evidence!, reference,
            now, DateOnly.FromDateTime(now.UtcDateTime));
        if (!preflight.DocumentaryPreflightReady || preflight.CandidateSnapshotDigestSha256 is null)
        {
            await tx.RollbackAsync(ct);
            return Results.Conflict(new { code = "CLOSURE_BLOCKED", reasons = preflight.BlockingReasons });
        }

        var actor = context.User.FindFirst("sub")!.Value;
        await db.Database.ExecuteSqlRawAsync("SELECT set_config('pmgm.assembly_closure_service', 'on', true)", ct);
        assembly.Status = "closed"; assembly.ClosedAtUtc = now; assembly.ClosedBySubject = actor;
        assembly.ClosureEvidenceDigest = preflight.CandidateSnapshotDigestSha256;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState is "23505" or "23514" or "40001" or "55000")
        { return Results.Conflict(new { code = "ASSEMBLY_CHANGED_RETRY_REVIEW" }); }
        await AuditAsync(context, db, "regimen.asamblea.cerrada", assemblyId,
            new { Voters = preflight.VotersCount, Digest = preflight.CandidateSnapshotDigestSha256, RosterReference = reference }, ct);
        await tx.CommitAsync(ct);
        return Results.Ok(new { assemblyId, closed = true, closedAtUtc = now, voters = preflight.VotersCount, digest = preflight.CandidateSnapshotDigestSha256, officiallyClosed = true });
    }
}

public sealed record AssemblyBalanceItem(Guid OrganizationId, decimal AmountPending);
public sealed record AssemblyBalancesRequest(string? SourceReference, string? Currency, IReadOnlyList<AssemblyBalanceItem>? Items);
public sealed record AssemblyCloseRequest(string? RosterReference);
