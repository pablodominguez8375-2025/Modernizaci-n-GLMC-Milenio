using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260909011000_AddLodgeInstruction")]
public partial class AddLodgeInstruction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lodge_instruction_sessions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                InstructionDate = table.Column<DateOnly>(type: "date", nullable: false),
                Grade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Topic = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ResponsibleOffice = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                InstructorMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_instruction_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_instruction_sessions_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_lodge_instruction_sessions_members_InstructorMemberId",
                    column: x => x.InstructorMemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "lodge_instruction_attendance_records",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstructionSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lodge_instruction_attendance_records", x => x.Id);
                table.ForeignKey(
                    name: "FK_lodge_instruction_attendance_records_lodge_instruction_sessions_InstructionSessionId",
                    column: x => x.InstructionSessionId,
                    principalSchema: "core",
                    principalTable: "lodge_instruction_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_lodge_instruction_attendance_records_members_MemberId",
                    column: x => x.MemberId,
                    principalSchema: "core",
                    principalTable: "members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_instruction_sessions_OrganizationId_InstructionDate",
            schema: "core",
            table: "lodge_instruction_sessions",
            columns: new[] { "OrganizationId", "InstructionDate" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_instruction_sessions_OrganizationId_Grade_InstructionDate",
            schema: "core",
            table: "lodge_instruction_sessions",
            columns: new[] { "OrganizationId", "Grade", "InstructionDate" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_instruction_sessions_InstructorMemberId",
            schema: "core",
            table: "lodge_instruction_sessions",
            column: "InstructorMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_lodge_instruction_attendance_records_InstructionSessionId_MemberId_RecordedAtUtc",
            schema: "core",
            table: "lodge_instruction_attendance_records",
            columns: new[] { "InstructionSessionId", "MemberId", "RecordedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_lodge_instruction_attendance_records_MemberId_RecordedAtUtc",
            schema: "core",
            table: "lodge_instruction_attendance_records",
            columns: new[] { "MemberId", "RecordedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "lodge_instruction_attendance_records",
            schema: "core");

        migrationBuilder.DropTable(
            name: "lodge_instruction_sessions",
            schema: "core");
    }
}
