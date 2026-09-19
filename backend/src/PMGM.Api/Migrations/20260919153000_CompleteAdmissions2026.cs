using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.Admissions;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(AdmissionsDbContext))]
[Migration("20260919153000_CompleteAdmissions2026")]
public partial class CompleteAdmissions2026 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "OriginObedienceRecognizedAsRegular",
            schema: "core",
            table: "admission_cases",
            type: "boolean",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StructuredDataJson",
            schema: "core",
            table: "admission_decisions",
            type: "jsonb",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "admission_commission_appointments",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AdmissionCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                AppointmentGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                AppointmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                SourceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                AppointedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admission_commission_appointments", x => x.Id);
                table.ForeignKey(
                    name: "FK_admission_commission_appointments_admission_cases_AdmissionCaseId",
                    column: x => x.AdmissionCaseId,
                    principalSchema: "core",
                    principalTable: "admission_cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_admission_commission_appointments_AdmissionCaseId_AppointmentGroupId",
            schema: "core",
            table: "admission_commission_appointments",
            columns: new[] { "AdmissionCaseId", "AppointmentGroupId" });

        migrationBuilder.CreateIndex(
            name: "IX_admission_commission_appointments_MemberId",
            schema: "core",
            table: "admission_commission_appointments",
            column: "MemberId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "admission_commission_appointments", schema: "core");
        migrationBuilder.DropColumn(name: "StructuredDataJson", schema: "core", table: "admission_decisions");
        migrationBuilder.DropColumn(name: "OriginObedienceRecognizedAsRegular", schema: "core", table: "admission_cases");
    }
}
