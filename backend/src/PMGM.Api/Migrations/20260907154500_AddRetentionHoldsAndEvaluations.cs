using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907154500_AddRetentionHoldsAndEvaluations")]
public partial class AddRetentionHoldsAndEvaluations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "data_retention_holds",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RetentionPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                EntityId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                AuthoritySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_data_retention_holds", x => x.Id);
                table.ForeignKey(
                    name: "FK_data_retention_holds_data_retention_policies_RetentionPolicyId",
                    column: x => x.RetentionPolicyId,
                    principalSchema: "core",
                    principalTable: "data_retention_policies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "data_retention_evaluations",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RetentionPolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                EntityId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                AnchorDate = table.Column<DateOnly>(type: "date", nullable: false),
                EvaluationDate = table.Column<DateOnly>(type: "date", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                RecommendedAction = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                BlockedByHold = table.Column<bool>(type: "boolean", nullable: false),
                RetentionHoldId = table.Column<Guid>(type: "uuid", nullable: true),
                Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EvaluatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_data_retention_evaluations", x => x.Id);
                table.ForeignKey(
                    name: "FK_data_retention_evaluations_data_retention_policies_RetentionPolicyId",
                    column: x => x.RetentionPolicyId,
                    principalSchema: "core",
                    principalTable: "data_retention_policies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_data_retention_holds_EntityType_EntityId_Status",
            schema: "core",
            table: "data_retention_holds",
            columns: new[] { "EntityType", "EntityId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_data_retention_holds_RetentionPolicyId_EffectiveFrom",
            schema: "core",
            table: "data_retention_holds",
            columns: new[] { "RetentionPolicyId", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_data_retention_evaluations_EntityType_EntityId_EvaluatedAtUtc",
            schema: "core",
            table: "data_retention_evaluations",
            columns: new[] { "EntityType", "EntityId", "EvaluatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_data_retention_evaluations_RetentionPolicyId_EvaluatedAtUtc",
            schema: "core",
            table: "data_retention_evaluations",
            columns: new[] { "RetentionPolicyId", "EvaluatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "data_retention_evaluations",
            schema: "core");

        migrationBuilder.DropTable(
            name: "data_retention_holds",
            schema: "core");
    }
}
