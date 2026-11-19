using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PMGM.Api.Migrations;
using PMGM.Api.Modules.SystemConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>#1044: forma de las dos migraciones del historial de bajas y resolución del estado a una fecha de corte.</summary>
public sealed class WorkshopStatusTransitionsShapeTests
{
    public const string UpSha256 = "43420213cd6bc5890c37781408297f87ac12af0a3c873b683c83acd2e5d2fc18";
    public const string DownSha256 = "04064ea0315b9237ea34e128a88e25cd6267e3c65f81d17d053cd3c27dfe291b";
    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [Fact]
    public void Table_migration_is_additive_native_ef_without_sql()
    {
        var up = new AddWorkshopStatusTransitions().UpOperations;
        Assert.DoesNotContain(up, x => x is SqlOperation);
        Assert.All(up, x => Assert.True(x is CreateTableOperation or CreateIndexOperation or AddCheckConstraintOperation, x.GetType().Name));
        var table = Assert.Single(up.OfType<CreateTableOperation>());
        Assert.Equal("core", table.Schema);
        Assert.Empty(table.ForeignKeys);   // sin FK: no calza la guardia T1 ni «datos asociados»
        Assert.DoesNotContain(table.Columns, c => System.Text.RegularExpressions.Regex.IsMatch(c.Name, "(organi[sz]ation|workshop|lodge|taller)id$", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        var down = new AddWorkshopStatusTransitions().DownOperations;
        Assert.DoesNotContain(down, x => x is DropTableOperation or DropColumnOperation or SqlOperation);
    }

    [Fact]
    public void Append_only_migration_runs_exactly_the_pinned_sql()
    {
        Assert.Equal(UpSha256, Sha(WorkshopStatusTransitionsSql.Up));
        Assert.Equal(DownSha256, Sha(WorkshopStatusTransitionsSql.Down));
        var migration = new AddWorkshopStatusTransitionsAppendOnly();
        Assert.Equal(WorkshopStatusTransitionsSql.Up, Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations)).Sql);
        Assert.Equal(WorkshopStatusTransitionsSql.Down, Assert.IsType<SqlOperation>(Assert.Single(migration.DownOperations)).Sql);
        var sql = WorkshopStatusTransitionsSql.Up.ToLowerInvariant();
        foreach (var forbidden in new[] { "insert into", "update core.", "delete from", "drop table", "alter table", "set_config", "current_setting" }) Assert.DoesNotContain(forbidden, sql);
        Assert.Contains("errcode = 'pm410'", sql); Assert.Contains("before truncate", sql);
        Assert.Contains("pm411", sql); Assert.Contains("pm412", sql); Assert.Contains("pm413", sql); Assert.Contains("before insert", sql); Assert.Contains("revoke update, delete, truncate", sql);
    }

    private static readonly DateTimeOffset T1 = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T3 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static WorkshopStatusTransition Tr(string from, string to, DateTimeOffset at) => new() { SubjectRef = Guid.NewGuid(), PreviousStatus = from, NextStatus = to, EffectiveAtUtc = at };

    [Fact]
    public void Resolve_reconstructs_active_retired_active_retired_at_every_cut()
    {
        // Sequence es identidad de BD: en memoria todas valen 0 y OrderBy es estable, así se conserva el orden dado.
        var list = new List<WorkshopStatusTransition> { Tr("active", "retired", T1), Tr("retired", "active", T2), Tr("active", "retired", T3) };
        WorkshopStatusAsOf At(int m, int d) => WorkshopStatusHistory.Resolve(list, "retired", T3, new DateOnly(2026, m, d));
        Assert.False(At(2, 28).Retired);                       // antes de la primera baja
        Assert.True(At(3, 1).Retired);                          // el mismo día de la baja (fin del día)
        Assert.Equal(T1, At(4, 1).RetiredAtUtc);
        Assert.False(At(6, 1).Retired);                         // reactivado ese día
        Assert.False(At(8, 31).Retired);
        Assert.True(At(9, 1).Retired); Assert.Equal(T3, At(12, 31).RetiredAtUtc);
    }

    [Fact]
    public void Resolve_without_history_uses_current_snapshot_and_never_invents_dates()
    {
        var none = new List<WorkshopStatusTransition>();
        Assert.False(WorkshopStatusHistory.Resolve(none, "active", null, new DateOnly(2026, 9, 9)).Retired);
        Assert.True(WorkshopStatusHistory.Resolve(none, "retired", T3, new DateOnly(2026, 9, 9)).Retired);
        // Fail-closed: antes de la baja registrada, sin historial, no se afirma «activo»: la historia es desconocida.
        var before = WorkshopStatusHistory.Resolve(none, "retired", T3, new DateOnly(2026, 8, 15));
        Assert.False(before.Known); Assert.True(before.Retired);
        var unknown = WorkshopStatusHistory.Resolve(none, "retired", null, new DateOnly(2020, 1, 1));
        Assert.False(unknown.Known); Assert.True(unknown.Retired); Assert.Null(unknown.RetiredAtUtc);             // fecha desconocida: de baja, sin fecha
    }

    [Fact]
    public void Resolve_first_transition_from_retired_means_it_started_retired()
    {
        var list = new List<WorkshopStatusTransition> { Tr("retired", "active", T2) };
        var early = WorkshopStatusHistory.Resolve(list, "active", null, new DateOnly(2026, 4, 1));
        Assert.True(early.Retired); Assert.False(early.Known);   // antes del baseline: solo se sabe que estaba de baja, no desde cuándo
        Assert.False(WorkshopStatusHistory.Resolve(list, "active", null, new DateOnly(2026, 7, 1)).Retired);
    }

    [Fact]
    public void Resolve_ignores_rows_with_unknown_dates_and_orders_by_effective_date_then_sequence()
    {
        var list = new List<WorkshopStatusTransition>
        {
            Tr("retired", "active", T3), Tr("active", "retired", T1),
            new() { SubjectRef = Guid.NewGuid(), PreviousStatus = "retired", NextStatus = "active", EffectiveAtUtc = T2, DateKnown = false }
        };
        Assert.True(WorkshopStatusHistory.Resolve(list, "active", null, new DateOnly(2026, 7, 1)).Retired);   // la fila sin fecha confiable no reactiva
        Assert.False(WorkshopStatusHistory.Resolve(list, "active", null, new DateOnly(2026, 10, 1)).Retired);
    }
}
