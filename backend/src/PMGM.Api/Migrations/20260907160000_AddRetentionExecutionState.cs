using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907160000_AddRetentionExecutionState")]
public partial class AddRetentionExecutionState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExecutedAction",
            schema: "core",
            table: "data_retention_evaluations",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExecutedAtUtc",
            schema: "core",
            table: "data_retention_evaluations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExecutedBySubject",
            schema: "core",
            table: "data_retention_evaluations",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExecutionEvidenceReference",
            schema: "core",
            table: "data_retention_evaluations",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExecutionResult",
            schema: "core",
            table: "data_retention_evaluations",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExecutedAction", schema: "core", table: "data_retention_evaluations");
        migrationBuilder.DropColumn(name: "ExecutedAtUtc", schema: "core", table: "data_retention_evaluations");
        migrationBuilder.DropColumn(name: "ExecutedBySubject", schema: "core", table: "data_retention_evaluations");
        migrationBuilder.DropColumn(name: "ExecutionEvidenceReference", schema: "core", table: "data_retention_evaluations");
        migrationBuilder.DropColumn(name: "ExecutionResult", schema: "core", table: "data_retention_evaluations");
    }
}
