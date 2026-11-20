using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;

namespace PMGM.Api.Modules.Bootstrap.InitialLoadApply;

/// <summary>
/// #694 — ONE transaction and ONE ledger for the whole batch (Talleres + Hermanos [+ Cargos, E3]).
/// The ledger only becomes applied after every stage succeeded; any stage failure rolls back
/// every entity, the reservation and the audit rows. Not in DI, no route, no flag.
/// The batch fingerprint MUST be recomputed by the caller (server side) from the canonical
/// bytes of the authorised plan; this class never accepts a client-supplied value as truth.
/// Actor and correlation come from the authenticated context, never from the CSV.
/// </summary>
public sealed class InitialLoadApplyOrchestrator(BootstrapDbContext boot)
{
    public sealed record Result(
        bool Applied, bool AlreadyApplied, string Code,
        int WorkshopsCreated, int MembersCreated, int MemberExactSkips,
        int OfficesCreated = 0, int OfficeExactSkips = 0);

    public async Task<Result> ApplyAsync(
        bool applyEnabled,
        string? verifiedBatchFingerprint,
        IReadOnlyList<InitialLoadWorkshopMatchPolicy.Incoming>? workshops,
        IReadOnlyList<InitialLoadMemberRowPolicy.Row>? members,
        IReadOnlyList<InitialLoadOfficeRowPolicy.Row>? offices,
        string actorSubject, string correlationId, CancellationToken ct)
    {
        // Pure validation first: nothing touches the database when any row is bad.
        var memberCheck = InitialLoadMemberRowPolicy.Inspect(members);
        if (!memberCheck.Valid) return Denied("MEMBERS_INVALID");

        var officeCheck = InitialLoadOfficeRowPolicy.Inspect(offices);
        if (!officeCheck.Valid) return Denied("OFFICES_INVALID");

        var officesCreated = 0;
        var officeSkips = 0;
        var membersCreated = 0;
        var memberSkips = 0;
        var service = new InitialLoadWorkshopApplyService(boot);
        var outcome = await service.ApplyAsync(
            applyEnabled, verifiedBatchFingerprint, workshops, actorSubject, correlationId, ct,
            async stageCt =>
            {
                // Same connection and same transaction as the workshops/ledger writes.
                var options = new DbContextOptionsBuilder<PmgmDbContext>()
                    .UseNpgsql(boot.Database.GetDbConnection()).Options;
                await using var core = new PmgmDbContext(options);
                await core.Database.UseTransactionAsync(
                    boot.Database.CurrentTransaction!.GetDbTransaction(), stageCt);

                var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
                foreach (var w in workshops!)
                {
                    var ids = await boot.Organizations.AsNoTracking()
                        .Where(o => o.Type == InitialLoadWorkshopApplyService.WorkshopType
                            && o.Number == w.Number && o.OrienteCode == w.OrienteCode)
                        .Select(o => o.Id).Take(2).ToListAsync(stageCt);
                    if (ids.Count != 1) return "WORKSHOP_RESOLUTION_AMBIGUOUS";
                    map[w.Code] = ids[0];
                }

                var step = await InitialLoadMembersStep.StageAsync(core, memberCheck.Rows, map, stageCt);
                membersCreated = step.Created;
                memberSkips = step.ExactSkips;
                if (step.DenyCode is not null) return step.DenyCode;

                // Cargos run last, still before the ledger can become applied.
                var offStep = await InitialLoadOfficesStep.StageAsync(
                    core, boot, officeCheck.Rows, memberCheck.Rows, map, stageCt);
                officesCreated = offStep.Created;
                officeSkips = offStep.ExactSkips;
                return offStep.DenyCode;
            });

        return new Result(outcome.Applied, outcome.AlreadyApplied, outcome.Code,
            outcome.WorkshopsCreated, outcome.Applied ? membersCreated : 0, outcome.Applied ? memberSkips : 0,
            outcome.Applied ? officesCreated : 0, outcome.Applied ? officeSkips : 0);
    }

    private static Result Denied(string code) => new(false, false, code, 0, 0, 0);
}
