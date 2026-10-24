using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PMGM.Api.Migrations;
using PMGM.Api.Modules.Bootstrap;
using PMGM.Api.Modules.Bootstrap.Entities;
using Xunit;

namespace PMGM.Api.Tests;

/// <summary>
/// The DB schema is fail-closed before any real initial-load endpoint exists.
/// No network/database or real personal data is needed for these model tests.
/// </summary>
public sealed class InitialLoadWindowModelTests
{
    [Fact]
    public void Singleton_starts_unopened_and_without_personal_or_legal_proof()
    {
        var window = new InitialLoadWindow();
        Assert.Equal(InitialLoadWindow.SingletonId, window.Id);
        Assert.Equal(InitialLoadWindow.NotOpened, window.Status);
        Assert.Equal(0, window.Version);
        Assert.Null(window.LegalApprovalDigest);
        Assert.Null(window.OpenedAtUtc);
        Assert.Null(window.ClosedAtUtc);
        Assert.Null(window.OpenedAuditEventId);
        Assert.Null(window.ClosedAuditEventId);
    }

    [Fact]
    public void Bootstrap_context_maps_audit_bound_singleton_and_revision_token()
    {
        var opts = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseNpgsql("Host=localhost;Database=pmgm_tests;Username=unused;Password=unused")
            .Options;
        using var db = new BootstrapDbContext(opts);
        var entity = db.Model.FindEntityType(typeof(InitialLoadWindow));
        Assert.NotNull(entity);
        Assert.Equal("core", entity!.GetSchema());
        Assert.Equal("initial_load_windows", entity.GetTableName());
        Assert.Single(entity.FindPrimaryKey()!.Properties);
        Assert.Equal(nameof(InitialLoadWindow.Id), entity.FindPrimaryKey()!.Properties[0].Name);
        Assert.True(entity.FindProperty(nameof(InitialLoadWindow.Version))!.IsConcurrencyToken);
        Assert.Equal(16, entity.FindProperty(nameof(InitialLoadWindow.Status))!.GetMaxLength());
        Assert.Equal(64, entity.FindProperty(nameof(InitialLoadWindow.LegalApprovalDigest))!.GetMaxLength());
        Assert.Equal(2, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk =>
            Assert.Equal("AuditEvent", fk.PrincipalEntityType.ClrType.Name));
    }

    [Fact]
    public void Migration_contains_default_closed_ledger_constraints_and_guards()
    {
        var migration = new AddInitialLoadWindow();
        var create = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("initial_load_windows", create.Name);
        Assert.Equal("core", create.Schema);
        Assert.Equal(2, create.ForeignKeys.Count);

        var sql = string.Join("\n", migration.UpOperations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("INSERT INTO core.initial_load_windows", sql);
        Assert.Contains("'not_opened', 0", sql);
        Assert.Contains("ck_initial_load_window_state", sql);
        Assert.Contains("trg_initial_load_window_no_unsafe_change", sql);
        Assert.Contains("trg_initial_load_window_no_truncate", sql);
        Assert.Contains("initial load legal and opening proof is immutable", sql);
        Assert.Contains("OLD.\"Status\" = 'open' AND NEW.\"Status\" = 'closed'", sql);
        Assert.DoesNotContain("Hermanos.csv", sql);
        Assert.DoesNotContain("identityNumber", sql);
    }
}
