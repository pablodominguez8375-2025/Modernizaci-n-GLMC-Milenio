using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.InitialLoadApply;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// #694 E2 + orchestrator on REAL PostgreSQL, SYNTHETIC data only. Each test uses a throw-away
/// database. The orchestrator owns the transaction and the single ledger; the flag is passed
/// explicitly and stays off in the application.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class InitialLoadApplyOrchestratorPostgreSqlTests
{
    private const string Digest = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";
    private static string? Conn => Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
    private static string Fp() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private static InitialLoadWorkshopMatchPolicy.Incoming W(string code, string number, string oriente) =>
        new(code, number, "Taller sintético", oriente, "Ciudad", "CL");

    private static InitialLoadMemberRowPolicy.Row M(
        string p, string rut, string workshop = "L1", string first = "Ana", string last = "Pérez",
        string grade = "master", string status = "active") =>
        new(p, "RUT", rut, first, last, "x@example.org", workshop, grade, status, "regular");

    private static BootstrapDbContext Boot(string cs) =>
        new(new DbContextOptionsBuilder<BootstrapDbContext>().UseNpgsql(cs).Options);

    private static PmgmDbContext Core(string cs) =>
        new(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(cs).Options);

    private static async Task PrepareAsync(string cs, CancellationToken ct)
    {
        await using (var core = Core(cs)) await core.Database.MigrateAsync(ct);
        await using var db = Boot(cs);
        await db.Database.MigrateAsync(ct);
        var id = Guid.NewGuid();
        db.AuditEvents.Add(new AuditEvent
        {
            Id = id, Action = "system.initial_load.window_open", EntityType = "initial_load_window",
            EntityId = "1", Result = "applied", CorrelationId = "synthetic-" + id.ToString("N")
        });
        await db.SaveChangesAsync(ct);
        if (!await db.OfficeDefinitions.AnyAsync(x => x.Code == "worshipful_master", ct))
        {
            db.OfficeDefinitions.Add(new PMGM.Api.Modules.Bootstrap.Entities.OfficeDefinition
            {
                Code = "worshipful_master", Name = "Venerable Maestro (sintético)", Category = "administrative",
                OrganizationType = BootstrapCodes.WorkshopType, IsPrimary = true, IsActive = true, SortOrder = 10
            });
            await db.SaveChangesAsync(ct);
        }
        var at = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE core.initial_load_windows
            SET "Status" = 'open', "Version" = 1, "LegalApprovalDigest" = {Digest},
                "OpenedAtUtc" = {at}, "OpenedAuditEventId" = {id}, "UpdatedAtUtc" = {at}
            WHERE "Id" = 1
            """, ct);
    }

    private static async Task<(int orgs, int people, int members, int memberships, int ledgers, int audits)> CountsAsync(
        string cs, CancellationToken ct)
    {
        await using var db = Boot(cs);
        await using var core = Core(cs);
        return (
            await db.Organizations.CountAsync(x => x.Origin == "initial_load", ct),
            await core.People.CountAsync(ct),
            await core.Members.CountAsync(ct),
            await core.Memberships.CountAsync(ct),
            await db.InitialLoadBatchLedgers.CountAsync(ct),
            await db.AuditEvents.CountAsync(x => x.Action.StartsWith("system.initial_load.batch_reserve")
                || x.Action.StartsWith("system.initial_load.workshops_apply"), ct));
    }

    private static async Task<InitialLoadApplyOrchestrator.Result> RunAsync(
        string cs, string fp, IReadOnlyList<InitialLoadWorkshopMatchPolicy.Incoming> w,
        IReadOnlyList<InitialLoadMemberRowPolicy.Row> m, CancellationToken ct, bool enabled = true,
        IReadOnlyList<InitialLoadOfficeRowPolicy.Row>? offices = null)
    {
        await using var db = Boot(cs);
        return await new InitialLoadApplyOrchestrator(db).ApplyAsync(
            enabled, fp, w, m, offices ?? [], "synthetic-master", "c-" + fp[..6], ct);
    }

    [Fact]
    public async Task Applies_workshops_and_members_in_one_transaction_with_one_applied_ledger_and_replays_idempotently()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        var fp = Fp();
        var w = new[] { W("L1", "9101", "SYA"), W("L2", "9102", "SYB") };
        var m = new[] { M("P1", "11.111.111-1"), M("P2", "22.222.222-2", "L2", "Luis", "Soto", "apprentice") };

        var first = await RunAsync(iso.Connection, fp, w, m, ct);
        Assert.True(first.Applied, first.Code);
        Assert.Equal(2, first.WorkshopsCreated);
        Assert.Equal(2, first.MembersCreated);
        Assert.Equal((2, 2, 2, 2, 1, 2), await CountsAsync(iso.Connection, ct));

        await using (var db = Boot(iso.Connection))
        {
            var ledger = await db.InitialLoadBatchLedgers.SingleAsync(ct);
            Assert.Equal("applied", ledger.Status);
            Assert.Equal(fp, ledger.Fingerprint);
            var audits = await db.AuditEvents.Where(x => x.Id == ledger.ReservedAuditEventId
                || x.Id == ledger.AppliedAuditEventId).ToListAsync(ct);
            Assert.All(audits, a => Assert.DoesNotContain("11111111", (a.MetadataJson ?? "") + (a.Summary ?? "")));
        }

        var replay = await RunAsync(iso.Connection, fp, w, m, ct);
        Assert.True(replay.AlreadyApplied);
        Assert.Equal((2, 2, 2, 2, 1, 2), await CountsAsync(iso.Connection, ct));
    }

    [Fact]
    public async Task Late_member_conflict_rolls_back_workshops_ledger_and_audit()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);

        // Batch 1 establishes person 11.111.111-1.
        var ok = await RunAsync(iso.Connection, Fp(), [W("L1", "9201", "SYA")], [M("P1", "11.111.111-1")], ct);
        Assert.True(ok.Applied, ok.Code);
        var baseline = await CountsAsync(iso.Connection, ct);

        // Batch 2: a NEW workshop plus the same RUT with a contradictory name -> whole batch denied.
        var bad = await RunAsync(iso.Connection, Fp(),
            [W("L1", "9201", "SYA"), W("L3", "9203", "SYC")],
            [M("P9", "11.111.111-1", "L1", "Otro", "Nombre")], ct);
        Assert.False(bad.Applied);
        Assert.Equal("MEMBER_CONFLICT_EXISTING_PERSON", bad.Code);
        Assert.Equal(baseline, await CountsAsync(iso.Connection, ct));
    }

    [Fact]
    public async Task Identical_existing_person_is_an_exact_skip_and_new_people_are_created()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        Assert.True((await RunAsync(iso.Connection, Fp(), [W("L1", "9301", "SYA")], [M("P1", "11.111.111-1")], ct)).Applied);

        var second = await RunAsync(iso.Connection, Fp(), [W("L1", "9301", "SYA")],
            [M("P1", "11.111.111-1"), M("P2", "22.222.222-2", "L1", "Eva", "Rojas", "fellowcraft")], ct);
        Assert.True(second.Applied, second.Code);
        Assert.Equal(0, second.WorkshopsCreated);
        Assert.Equal(1, second.MembersCreated);
        Assert.Equal(1, second.MemberExactSkips);
        Assert.Equal((1, 2, 2, 2, 2, 4), await CountsAsync(iso.Connection, ct));
    }

    [Fact]
    public async Task Unknown_workshop_reference_and_invalid_rows_write_nothing()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        var baseline = await CountsAsync(iso.Connection, ct);

        var unknown = await RunAsync(iso.Connection, Fp(), [W("L1", "9401", "SYA")],
            [M("P1", "11.111.111-1", "L-NOPE")], ct);
        Assert.Equal("MEMBER_WORKSHOP_UNKNOWN", unknown.Code);

        foreach (var bad in new[]
        {
            M("P1", "11.111.111-2"),                    // bad check digit
            M("P1", "11.111.111-1", grade: "Maestro"),  // non-canonical grade
            M("P1", "11.111.111-1", status: "deceased"),// status without persisted semantics
            M("P1", "11.111.111-1") with { IdentityType = "PASAPORTE" }
        })
        {
            var r = await RunAsync(iso.Connection, Fp(), [W("L1", "9401", "SYA")], [bad], ct);
            Assert.False(r.Applied);
            Assert.Equal("MEMBERS_INVALID", r.Code);
        }
        Assert.Equal(baseline, await CountsAsync(iso.Connection, ct));
    }

    private static InitialLoadOfficeRowPolicy.Row O(
        string p = "P1", string w = "L1", string code = "worshipful_master", string date = "2026-03-01", string period = "2026") =>
        new(p, w, code, date, period);

    [Fact]
    public async Task Applies_offices_and_a_late_failure_in_cargos_rolls_back_all_three_entities_ledger_and_audit()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        var w = new[] { W("L1", "9601", "SYA") };
        var m = new[] { M("P1", "11.111.111-1") };

        // Late failure: valid workshops + members, but officeCode is not an exact active definition.
        var baseline = await CountsAsync(iso.Connection, ct);
        var bad = await RunAsync(iso.Connection, Fp(), w, m, ct, offices: [O(code: "no_such_office")]);
        Assert.False(bad.Applied);
        Assert.Equal("OFFICE_CODE_NOT_EXACT", bad.Code);
        Assert.Equal(baseline, await CountsAsync(iso.Connection, ct));
        await using (var core = Core(iso.Connection))
            Assert.Equal(0, await core.OfficeAssignments.CountAsync(ct));

        // Other unmappable shapes also deny with zero writes.
        foreach (var (rows, code) in new (InitialLoadOfficeRowPolicy.Row[], string)[]
        {
            ([O(p: "P-NOPE")], "OFFICE_PERSON_UNKNOWN"),
            ([O(w: "L2")], "OFFICE_MEMBER_WORKSHOP_MISMATCH"),
        })
        {
            var r = await RunAsync(iso.Connection, Fp(), w, m, ct, offices: rows);
            Assert.Equal(code, r.Code);
        }
        foreach (var invalid in new[] { O(period: ""), O(date: "01-03-2026"), O(date: "2026-13-40") })
            Assert.Equal("OFFICES_INVALID", (await RunAsync(iso.Connection, Fp(), w, m, ct, offices: [invalid])).Code);
        Assert.Equal(baseline, await CountsAsync(iso.Connection, ct));

        // Happy path, then the same rows under a NEW fingerprint (distinct batch) = exact entity skip,
        // NOT an idempotent replay (that is covered by Same_fingerprint_replay_*).
        var ok = await RunAsync(iso.Connection, Fp(), w, m, ct, offices: [O()]);
        Assert.True(ok.Applied, ok.Code);
        Assert.Equal(1, ok.OfficesCreated);
        var again = await RunAsync(iso.Connection, Fp(), w, m, ct, offices: [O()]);
        Assert.True(again.Applied, again.Code);
        Assert.Equal(0, again.OfficesCreated);
        Assert.Equal(1, again.OfficeExactSkips);

        // Same member/office/period with a different start date is a conflict: nothing else changes.
        var counts = await CountsAsync(iso.Connection, ct);
        var conflict = await RunAsync(iso.Connection, Fp(), w, m, ct, offices: [O(date: "2026-04-01")]);
        Assert.Equal("OFFICE_CONFLICT_EXISTING_ASSIGNMENT", conflict.Code);
        Assert.Equal(counts, await CountsAsync(iso.Connection, ct));
        await using var check = Core(iso.Connection);
        Assert.Equal(1, await check.OfficeAssignments.CountAsync(ct));
    }

    [Fact]
    public async Task Same_fingerprint_replay_of_a_full_batch_is_already_applied_with_zero_writes()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        var fp = Fp();
        var w = new[] { W("L1", "9801", "SYA") };
        var m = new[] { M("P1", "11.111.111-1") };
        var o = new[] { O() };

        var first = await RunAsync(iso.Connection, fp, w, m, ct, offices: o);
        Assert.True(first.Applied, first.Code);
        var afterFirst = await CountsAsync(iso.Connection, ct);

        var replay = await RunAsync(iso.Connection, fp, w, m, ct, offices: o);
        Assert.False(replay.Applied);
        Assert.True(replay.AlreadyApplied);
        Assert.Equal("ALREADY_APPLIED", replay.Code);
        Assert.Equal(afterFirst, await CountsAsync(iso.Connection, ct));
        await using var db = Boot(iso.Connection);
        Assert.Equal(1, await db.InitialLoadBatchLedgers.CountAsync(x => x.Fingerprint == fp, ct));
    }

    [Fact]
    public async Task Distinct_fingerprint_with_identical_content_skips_entities_without_mutating_them()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        var w = new[] { W("L1", "9802", "SYA") };
        var m = new[] { M("P1", "11.111.111-1") };
        var o = new[] { O() };
        Assert.True((await RunAsync(iso.Connection, Fp(), w, m, ct, offices: o)).Applied);

        List<Guid> ids;
        await using (var core = Core(iso.Connection))
            ids = await core.OfficeAssignments.Select(x => x.Id).ToListAsync(ct);

        var second = await RunAsync(iso.Connection, Fp(), w, m, ct, offices: o);
        Assert.True(second.Applied, second.Code);
        Assert.Equal((0, 0, 1, 0, 1), (second.WorkshopsCreated, second.MembersCreated, second.MemberExactSkips,
            second.OfficesCreated, second.OfficeExactSkips));

        await using var core2 = Core(iso.Connection);
        Assert.Equal(1, await core2.People.CountAsync(ct));
        Assert.Equal(1, await core2.Members.CountAsync(ct));
        Assert.Equal(1, await core2.Memberships.CountAsync(ct));
        Assert.Equal(ids, await core2.OfficeAssignments.Select(x => x.Id).ToListAsync(ct));
        await using var db = Boot(iso.Connection);
        Assert.Equal(2, await db.InitialLoadBatchLedgers.CountAsync(ct));
    }

    [Fact]
    public async Task Three_canonical_csv_sheets_flow_through_adapter_into_the_orchestrator()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);

        var plan = InitialLoadPlanAdapter.Build(InitialLoadPlanAdapterTests.Sheets()).Plan!;
        var result = await RunAsync(iso.Connection, Fp(), plan.Workshops, plan.Members, ct, offices: plan.Offices);
        Assert.True(result.Applied, result.Code);
        Assert.Equal((1, 1, 1, 1, 1, 2), await CountsAsync(iso.Connection, ct));

        await using var core = Core(iso.Connection);
        var office = await core.OfficeAssignments.SingleAsync(ct);
        Assert.Equal("worshipful_master", office.OfficeType);
        Assert.Equal("2026", office.Period);
        Assert.Equal(new DateOnly(2026, 3, 1), office.StartDate);
        var member = await core.Members.SingleAsync(ct);
        Assert.Equal("master", member.CurrentDegree);
        var membership = await core.Memberships.SingleAsync(ct);
        Assert.Equal("regular", membership.MembershipType);
        Assert.Equal("active", membership.Status);
    }

    [Fact]
    public async Task Three_parallel_identical_full_batches_apply_exactly_once()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        var fp = Fp();
        var w = new[] { W("L1", "9701", "SYA"), W("L2", "9702", "SYB") };
        var m = new[] { M("P1", "11.111.111-1"), M("P2", "22.222.222-2", "L2", "Luis", "Soto") };
        var o = new[] { O(), O("P2", "L2") };

        var results = await Task.WhenAll(
            RunAsync(iso.Connection, fp, w, m, ct, offices: o),
            RunAsync(iso.Connection, fp, w, m, ct, offices: o),
            RunAsync(iso.Connection, fp, w, m, ct, offices: o));
        Assert.Equal(1, results.Count(r => r.Applied));
        Assert.All(results.Where(r => !r.Applied), r =>
            Assert.Contains(r.Code, new[] { "ALREADY_APPLIED", "CONCURRENT_OR_CONFLICTING_BATCH" }));
        Assert.Equal((2, 2, 2, 2, 1, 2), await CountsAsync(iso.Connection, ct));
        await using var core = Core(iso.Connection);
        Assert.Equal(2, await core.OfficeAssignments.CountAsync(ct));
    }

    [Fact]
    public async Task Disabled_flag_and_arbitrary_fingerprint_deny_without_writes()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        var baseline = await CountsAsync(iso.Connection, ct);
        var w = new[] { W("L1", "9501", "SYA") };
        var m = new[] { M("P1", "11.111.111-1") };

        Assert.Equal("APPLY_DISABLED", (await RunAsync(iso.Connection, Fp(), w, m, ct, enabled: false)).Code);
        Assert.Equal("INVALID_VERIFIED_FINGERPRINT", (await RunAsync(iso.Connection, "not-a-hash", w, m, ct)).Code);
        Assert.Equal(baseline, await CountsAsync(iso.Connection, ct));
    }

    private static string SyntheticRut(int n)
    {
        var body = 30_000_000 + n;
        var sum = 0; var f = 2;
        for (var x = body; x > 0; x /= 10) { sum += (x % 10) * f; f = f == 7 ? 2 : f + 1; }
        var dv = 11 - sum % 11;
        var d = dv == 11 ? "0" : dv == 10 ? "K" : dv.ToString();
        var t = body.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
        return t + "-" + d;
    }

    [Fact]
    public async Task Second_batch_of_the_same_workshop_reuses_it_and_adds_members_without_duplicating()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, ct);
        var w = new[] { W("L1", "9901", "SYA") };
        var first = Enumerable.Range(1, 30).Select(i => M("P" + i, SyntheticRut(i), "L1", "N" + i, "S" + i)).ToArray();
        var second = Enumerable.Range(31, 10).Select(i => M("P" + i, SyntheticRut(i), "L1", "N" + i, "S" + i)).ToArray();

        var r1 = await RunAsync(iso.Connection, Fp(), w, first, ct);
        Assert.True(r1.Applied, r1.Code);
        Assert.Equal((1, 30), (r1.WorkshopsCreated, r1.MembersCreated));

        Guid workshopId;
        await using (var db = Boot(iso.Connection))
            workshopId = await db.Organizations.Where(x => x.Origin == "initial_load").Select(x => x.Id).SingleAsync(ct);

        var r2 = await RunAsync(iso.Connection, Fp(), w, second, ct);
        Assert.True(r2.Applied, r2.Code);
        Assert.Equal((0, 10), (r2.WorkshopsCreated, r2.MembersCreated));

        await using var db2 = Boot(iso.Connection);
        Assert.Equal(workshopId, await db2.Organizations.Where(x => x.Origin == "initial_load").Select(x => x.Id).SingleAsync(ct));
        await using var core = Core(iso.Connection);
        Assert.Equal(40, await core.People.CountAsync(ct));
        Assert.Equal(40, await core.Members.CountAsync(ct));
        Assert.Equal(40, await core.Memberships.CountAsync(ct));
        Assert.Equal(2, await db2.InitialLoadBatchLedgers.CountAsync(ct));

        // A repeated second batch is a pure replay: zero writes.
        var replayFp = await db2.InitialLoadBatchLedgers.OrderByDescending(x => x.CreatedAtUtc).Select(x => x.Fingerprint).FirstAsync(ct);
        var replay = await RunAsync(iso.Connection, replayFp, w, second, ct);
        Assert.True(replay.AlreadyApplied);
        Assert.Equal(40, await core.Members.CountAsync(ct));
    }

    private sealed class IsolatedDb : IAsyncDisposable
    {
        private readonly string _admin;
        private readonly string _name;
        public string Connection { get; }
        private IsolatedDb(string admin, string name, string connection) { _admin = admin; _name = name; Connection = connection; }

        public static async Task<IsolatedDb> CreateAsync(string adminConnection, CancellationToken ct)
        {
            var name = "pmgm_orch_" + Guid.NewGuid().ToString("N")[..12];
            await using (var conn = new NpgsqlConnection(adminConnection))
            {
                await conn.OpenAsync(ct);
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn);
                await create.ExecuteNonQueryAsync(ct);
            }
            var cs = new NpgsqlConnectionStringBuilder(adminConnection) { Database = name }.ConnectionString;
            return new IsolatedDb(adminConnection, name, cs);
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await using var conn = new NpgsqlConnection(_admin);
            await conn.OpenAsync(CancellationToken.None);
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", conn);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
