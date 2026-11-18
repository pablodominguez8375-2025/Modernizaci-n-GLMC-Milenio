using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PMGM.Api.Migrations;
using Xunit;

namespace PMGM.Api.Tests.Authorization;

/// <summary>
/// #1035/#1038: la migración del registro de Talleres debe ser DEMOSTRABLEMENTE aditiva para el clasificador fail-closed
/// de #1036: solo AddColumn (con DEFAULT o nulable) y un AddCheckConstraint sobre core.organizations; nunca SQL manual.
/// </summary>
public sealed class WorkshopRegistryMigrationShapeTests
{
    private static IReadOnlyList<MigrationOperation> Up() => new AddWorkshopRegistryState().UpOperations;
    private static IReadOnlyList<MigrationOperation> Down() => new AddWorkshopRegistryState().DownOperations;

    [Fact]
    public void Up_contains_no_manual_sql_and_only_allowlisted_operations_on_organizations()
    {
        var operations = Up();
        Assert.NotEmpty(operations);
        Assert.DoesNotContain(operations, x => x is SqlOperation);
        foreach (var operation in operations)
        {
            switch (operation)
            {
                case AddColumnOperation column:
                    Assert.Equal("core", column.Schema);
                    Assert.Equal("organizations", column.Table);
                    // aditiva y compatible con API N: nulable o con DEFAULT
                    Assert.True(column.IsNullable || column.DefaultValue is not null, column.Name + " debe ser nulable o tener DEFAULT");
                    break;
                case AddCheckConstraintOperation check:
                    Assert.Equal("core", check.Schema);
                    Assert.Equal("organizations", check.Table);
                    Assert.Equal("CK_organizations_Status", check.Name);
                    break;
                default:
                    Assert.Fail("Operación no permitida en una migración aditiva: " + operation.GetType().Name);
                    break;
            }
        }
    }

    [Fact]
    public void Up_adds_exactly_the_four_registry_columns_and_one_check()
    {
        var names = Up().OfType<AddColumnOperation>().Select(x => x.Name).OrderBy(x => x).ToArray();
        Assert.Equal(["Origin", "RetiredAtUtc", "RetiredReason", "Status"], names);
        Assert.Single(Up().OfType<AddCheckConstraintOperation>());
        Assert.Equal(5, Up().Count);
    }

    [Fact]
    public void Down_is_conservative_it_only_drops_the_check_and_never_data()
    {
        var operations = Down();
        Assert.Single(operations);
        Assert.IsType<DropCheckConstraintOperation>(operations[0]);
        Assert.DoesNotContain(operations, x => x is DropColumnOperation or DropTableOperation or SqlOperation);
    }

    [Fact]
    public void Shape_check_would_reject_manual_sql_or_a_destructive_operation()
    {
        // Prueba negativa: el mismo criterio detecta SQL manual y operaciones destructivas.
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        builder.Sql("ALTER TABLE core.organizations ADD COLUMN \"X\" int");
        builder.DropColumn(name: "Origin", schema: "core", table: "organizations");
        Assert.Contains(builder.Operations, x => x is SqlOperation);
        Assert.Contains(builder.Operations, x => x is DropColumnOperation);
        Assert.DoesNotContain(Up(), x => x is SqlOperation or DropColumnOperation);
    }
}
