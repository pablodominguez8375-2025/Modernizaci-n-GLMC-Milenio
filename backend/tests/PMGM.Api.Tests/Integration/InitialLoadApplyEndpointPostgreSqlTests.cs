using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMGM.Api.Data;
using PMGM.Api.Modules.Audit.Entities;
using PMGM.Api.Modules.Authorization;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.InitialLoadApply;
using Xunit;

namespace PMGM.Api.Tests.Integration;

/// <summary>
/// #694 apply boundary on REAL PostgreSQL with SYNTHETIC CSV only. The endpoint core is exercised with a
/// trusted actor; the HTTP shell (flag off, non-Master) is covered through the test web factory.
/// </summary>
[Collection(PostgresIntegrationCollection.Name)]
public sealed class InitialLoadApplyEndpointPostgreSqlTests
{
    private const string Digest = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";
    private static readonly byte[] Secret = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static string? Conn => Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");

    private static Dictionary<string, string> Csv(
        string status = "active", string oriente = "SYA", string number = "9901", string period = "2026")
    {
        static string Header(string file) =>
            string.Join(",", InitialLoadTemplateEndpoints.Sheets.Single(s => s.Name == file).Columns) + "\r\n";
        return new Dictionary<string, string>
        {
            ["Talleres.csv"] = Header("Talleres.csv") + $"t1,{number},Taller sintético,{oriente},Santiago,Chile\r\n",
            ["Hermanos.csv"] = Header("Hermanos.csv") +
                $"h1,rut,12.345.678-5,Ana,Pérez,qa@example.invalid,t1,3,{status},regular\r\n",
            ["CargosVigentes.csv"] = Header("CargosVigentes.csv") + $"h1,t1,worshipful_master,2026-03-01,{period}\r\n"
        };
    }

    private static byte[] Body(Dictionary<string, string> files, string? receipt) =>
        JsonSerializer.SerializeToUtf8Bytes(new { files, receipt });

    private static string Receipt(Dictionary<string, string> files, DateTimeOffset? at = null) =>
        InitialLoadPreflightReceiptPolicy.Issue(files, Secret, at ?? DateTimeOffset.UtcNow)!.Token;

    private static BootstrapDbContext Boot(string cs) =>
        new(new DbContextOptionsBuilder<BootstrapDbContext>().UseNpgsql(cs).Options);

    private static async Task PrepareAsync(string cs, bool open, CancellationToken ct)
    {
        await using (var core = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(cs).Options))
            await core.Database.MigrateAsync(ct);
        await using var db = Boot(cs);
        await db.Database.MigrateAsync(ct);
        if (!await db.OfficeDefinitions.AnyAsync(x => x.Code == "worshipful_master", ct))
        {
            db.OfficeDefinitions.Add(new PMGM.Api.Modules.Bootstrap.Entities.OfficeDefinition
            {
                Code = "worshipful_master", Name = "Venerable Maestro (sintético)", Category = "administrative",
                OrganizationType = BootstrapCodes.WorkshopType, IsPrimary = true, IsActive = true, SortOrder = 10
            });
            await db.SaveChangesAsync(ct);
        }
        if (!open) return;
        var id = Guid.NewGuid();
        db.AuditEvents.Add(new AuditEvent
        {
            Id = id, Action = "system.initial_load.window_open", EntityType = "initial_load_window",
            EntityId = "1", Result = "applied", CorrelationId = "synthetic-" + id.ToString("N")
        });
        await db.SaveChangesAsync(ct);
        var at = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE core.initial_load_windows
            SET "Status" = 'open', "Version" = 1, "LegalApprovalDigest" = {Digest},
                "OpenedAtUtc" = {at}, "OpenedAuditEventId" = {id}, "UpdatedAtUtc" = {at}
            WHERE "Id" = 1
            """, ct);
    }

    private static async Task<(int Status, string Json)> CallAsync(
        string cs, byte[] body, string actor = "synthetic-master-sub", string corr = "corr-1",
        DateTimeOffset? now = null, CancellationToken ct = default)
    {
        await using var db = Boot(cs);
        var outcome = await InitialLoadApplyEndpoint.ApplyVerifiedAsync(
            actor, corr, body, Secret, db, new InitialLoadApplyOrchestrator(db), now ?? DateTimeOffset.UtcNow, ct);
        return (outcome.Status, JsonSerializer.Serialize(outcome.Payload));
    }

    private static async Task<(int orgs, int people, int offices, int ledgers)> CountsAsync(string cs, CancellationToken ct)
    {
        await using var db = Boot(cs);
        await using var core = new PmgmDbContext(new DbContextOptionsBuilder<PmgmDbContext>().UseNpgsql(cs).Options);
        return (await db.Organizations.CountAsync(x => x.Origin == "initial_load", ct),
            await core.People.CountAsync(ct), await core.OfficeAssignments.CountAsync(ct),
            await db.InitialLoadBatchLedgers.CountAsync(ct));
    }

    [Fact]
    public async Task Preview_marks_legacy_rows_as_readable_but_not_applicable()
    {
        foreach (var (files, code) in new[]
        {
            (Csv(oriente: ""), "ORIENTE_REQUIRED_FOR_APPLY"),
            (Csv(status: "inactive"), "STATUS_NOT_APPLICABLE"),
        })
        {
            Assert.True(InitialLoadCsvPreviewPolicy.Inspect(files).Valid); // readable preview
            var sheets = InitialLoadCsvPreviewPolicy.TryParseSheets(files);
            var result = InitialLoadApplicabilityPolicy.Inspect(sheets);
            Assert.False(result.Applicable);
            Assert.Contains(result.Blockers, b => b.Code == code);
        }
        Assert.True(InitialLoadApplicabilityPolicy.Inspect(InitialLoadCsvPreviewPolicy.TryParseSheets(Csv())).Applicable);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Valid_receipt_applies_everything_audits_server_actor_and_replays_without_writes()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, open: true, ct);
        var files = Csv();
        var body = Body(files, Receipt(files));

        var first = await CallAsync(iso.Connection, body, actor: "actor-from-jwt", corr: "trace-123", ct: ct);
        Assert.Equal(200, first.Status);
        Assert.Contains("\"applied\":true", first.Json);
        Assert.Equal((1, 1, 1, 1), await CountsAsync(iso.Connection, ct));

        await using (var db = Boot(iso.Connection))
        {
            var audits = await db.AuditEvents.Where(x => x.Action.StartsWith("system.initial_load.batch_reserve")
                || x.Action.StartsWith("system.initial_load.workshops_apply")).ToListAsync(ct);
            Assert.All(audits, a =>
            {
                Assert.Equal("actor-from-jwt", a.ActorSubject);
                Assert.Equal("trace-123", a.CorrelationId);
                Assert.DoesNotContain("12.345.678", (a.MetadataJson ?? "") + (a.Summary ?? ""));
            });
        }

        // Same body (same bytes) again, even with a fresh receipt: same server-derived fingerprint.
        var replay = await CallAsync(iso.Connection, Body(files, Receipt(files)), ct: ct);
        Assert.Equal(200, replay.Status);
        Assert.Contains("\"alreadyApplied\":true", replay.Json);
        Assert.Equal((1, 1, 1, 1), await CountsAsync(iso.Connection, ct));
    }

    [Fact]
    public async Task Tampered_csv_forged_expired_or_missing_receipt_and_client_fingerprint_are_rejected_with_zero_writes()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, open: true, ct);
        var files = Csv();
        var receipt = Receipt(files);

        var tampered = Csv(number: "9902"); // different bytes, receipt of the original
        var forgedToken = receipt[..^4] + (receipt.EndsWith("abcd") ? "0000" : "abcd");
        var arbitraryFingerprint = new string('a', 64);
        foreach (var body in new[]
        {
            Body(tampered, receipt),
            Body(files, forgedToken),
            Body(files, null),
            Body(files, arbitraryFingerprint), // a client-supplied "fingerprint" is not a receipt
            Encoding.UTF8.GetBytes("{not json"),
        })
            Assert.InRange((await CallAsync(iso.Connection, body, ct: ct)).Status, 400, 422);

        var expired = await CallAsync(iso.Connection, Body(files, Receipt(files, DateTimeOffset.UtcNow.AddMinutes(-30))), ct: ct);
        Assert.Equal(422, expired.Status);
        Assert.Contains("RECEIPT_EXPIRED", expired.Json);
        Assert.Equal((0, 0, 0, 0), await CountsAsync(iso.Connection, ct));
    }

    [Fact]
    public async Task Not_applicable_batch_and_closed_window_deny_with_zero_writes()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, open: true, ct);

        var inactive = Csv(status: "inactive");
        var notApplicable = await CallAsync(iso.Connection, Body(inactive, Receipt(inactive)), ct: ct);
        Assert.Equal(422, notApplicable.Status);
        Assert.Contains("BATCH_NOT_APPLICABLE", notApplicable.Json);
        Assert.Contains("STATUS_NOT_APPLICABLE", notApplicable.Json);

        await using var closed = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(closed.Connection, open: false, ct);
        var files = Csv();
        var denied = await CallAsync(closed.Connection, Body(files, Receipt(files)), ct: ct);
        Assert.Equal(409, denied.Status);
        Assert.Contains("WINDOW_NOT_OPEN", denied.Json);
        Assert.Equal((0, 0, 0, 0), await CountsAsync(iso.Connection, ct));
        Assert.Equal((0, 0, 0, 0), await CountsAsync(closed.Connection, ct));
    }

    [Fact]
    public async Task Three_parallel_identical_requests_apply_exactly_once()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        await using var iso = await IsolatedDb.CreateAsync(Conn!, ct);
        await PrepareAsync(iso.Connection, open: true, ct);
        var files = Csv();
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(i =>
            CallAsync(iso.Connection, Body(files, Receipt(files)), corr: "p" + i, ct: ct)));
        Assert.Equal(1, results.Count(r => r.Json.Contains("\"applied\":true")));
        Assert.All(results, r => Assert.Contains(r.Status, new[] { 200, 409 }));
        Assert.Equal((1, 1, 1, 1), await CountsAsync(iso.Connection, ct));
    }

    [Fact]
    public async Task Http_shell_is_404_when_disabled_and_forbidden_for_non_master_when_enabled()
    {
        if (string.IsNullOrWhiteSpace(Conn)) return;
        var ct = TestContext.Current.CancellationToken;
        var payload = new { files = Csv(), receipt = "x" };

        using (var factory = new LodgeManagementWebApplicationFactory(Conn!))
        using (var client = factory.CreateClient())
        {
            client.DefaultRequestHeaders.Add("X-Test-Role", InstitutionalRoles.GranLogiaAdmin);
            using var off = await client.PostAsJsonAsync("/api/platform/bootstrap/initial-load/apply", payload, ct);
            Assert.Equal(HttpStatusCode.NotFound, off.StatusCode);
        }

        using var enabled = new LodgeManagementWebApplicationFactory(Conn!)
            .WithWebHostBuilder(b => b.UseSetting("InitialLoad:ApplyEnabled", "true"));
        using var client2 = enabled.CreateClient();
        client2.DefaultRequestHeaders.Add("X-Test-Role", InstitutionalRoles.GranLogiaAdmin);
        using var forbidden = await client2.PostAsJsonAsync("/api/platform/bootstrap/initial-load/apply", payload, ct);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private sealed class IsolatedDb : IAsyncDisposable
    {
        private readonly string _admin;
        private readonly string _name;
        public string Connection { get; }
        private IsolatedDb(string admin, string name, string connection) { _admin = admin; _name = name; Connection = connection; }

        public static async Task<IsolatedDb> CreateAsync(string adminConnection, CancellationToken ct)
        {
            var name = "pmgm_apiapply_" + Guid.NewGuid().ToString("N")[..12];
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
