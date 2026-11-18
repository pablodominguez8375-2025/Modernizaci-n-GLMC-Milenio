using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PMGM.Api.Migrations;
using PMGM.Api.Modules.SystemConfiguration;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>
/// #1039 T1: el SQL de la guardia esta FIJADO por digest. Cambiarlo exige nueva revision (ChatGPT) y nuevo digest firmado:
/// esta prueba falla a proposito ante cualquier edicion. Es SQL manual => migracion MANUAL, nunca auto-aplicable.
/// </summary>
public sealed class WorkshopRetiredGuardShapeTests
{
    public const string UpSha256 = "d06e991ab244d258e1d17e9b8ad60c6b4f566cc82fbe7d3f4619f345b5500196";
    public const string DownSha256 = "96a9c6bdc1199e1bbe54a03639601f59bc14c4ab14e75b2255ff269ce7569b95";

    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [Fact]
    public void Sql_text_is_pinned_by_digest()
    {
        Assert.Equal(UpSha256, Sha(WorkshopRetiredGuardSql.Up));
        Assert.Equal(DownSha256, Sha(WorkshopRetiredGuardSql.Down));
    }

    [Fact]
    public void Migration_runs_exactly_the_pinned_sql_and_nothing_else()
    {
        var migration = new AddWorkshopRetiredGuard();
        var up = Assert.Single(migration.UpOperations);
        Assert.Equal(WorkshopRetiredGuardSql.Up, Assert.IsType<SqlOperation>(up).Sql);
        var down = Assert.Single(migration.DownOperations);
        Assert.Equal(WorkshopRetiredGuardSql.Down, Assert.IsType<SqlOperation>(down).Sql);
    }

    [Fact]
    public void Sql_has_no_session_bypass_and_no_data_changes()
    {
        var sql = WorkshopRetiredGuardSql.Up.ToLowerInvariant();
        foreach (var forbidden in new[] { "set_config", "current_setting", "session_user", "current_user", "update core.", "delete from", "insert into", "truncate", "drop table", "alter table" })
            Assert.DoesNotContain(forbidden, sql);
        Assert.Contains("errcode = 'pm409'", sql);
        Assert.Contains("for share", sql);
    }
}
