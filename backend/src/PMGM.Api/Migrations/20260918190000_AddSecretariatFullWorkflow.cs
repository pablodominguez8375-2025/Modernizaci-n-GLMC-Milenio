using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMGM.Api.Migrations;

public partial class AddSecretariatFullWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Rut",
            schema: "core",
            table: "people",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_people_Rut",
            schema: "core",
            table: "people",
            column: "Rut",
            unique: true);

        migrationBuilder.AddColumn<string>(
            name: "CurrentDegree",
            schema: "core",
            table: "members",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AlterColumn<DateOnly>(
            name: "StartDate",
            schema: "core",
            table: "memberships",
            type: "date",
            nullable: true,
            oldClrType: typeof(DateOnly),
            oldType: "date");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "StartDate",
            schema: "core",
            table: "office_assignments",
            type: "date",
            nullable: true,
            oldClrType: typeof(DateOnly),
            oldType: "date");

        migrationBuilder.AddColumn<string>(
            name: "CeremonyType",
            schema: "core",
            table: "lodge_meetings",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Modality",
            schema: "core",
            table: "lodge_meetings",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "in_person");

        migrationBuilder.AddColumn<string>(
            name: "LocationReference",
            schema: "core",
            table: "lodge_meetings",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "VirtualAccessReference",
            schema: "core",
            table: "lodge_meetings",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlanchaKind",
            schema: "core",
            table: "secretariat_documents",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "historical_member_intakes",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                FirstNames = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                LastNames = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Rut = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                InstitutionalNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                Phone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                CurrentDegree = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                MembershipStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                InitiationDate = table.Column<DateOnly>(type: "date", nullable: true),
                WageIncreaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                ExaltationDate = table.Column<DateOnly>(type: "date", nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Revision = table.Column<int>(type: "integer", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ApprovedMemberId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_historical_member_intakes", x => x.Id));

        migrationBuilder.CreateTable(
            name: "lodge_administrative_meetings",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MeetingDate = table.Column<DateOnly>(type: "date", nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Purpose = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                HeldAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_lodge_administrative_meetings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "lodge_secretariat_records",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                RecordType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SourceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                WorkPaperDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                WorkPaperAuthorMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                ExtractDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                FullMinuteDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SubmittedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_lodge_secretariat_records", x => x.Id));

        migrationBuilder.CreateTable(
            name: "historical_member_intake_offices",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IntakeId = table.Column<Guid>(type: "uuid", nullable: false),
                OfficeType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Period = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                IsCurrent = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_historical_member_intake_offices", x => x.Id);
                table.ForeignKey(
                    name: "FK_historical_member_intake_offices_historical_member_intakes_IntakeId",
                    column: x => x.IntakeId,
                    principalSchema: "core",
                    principalTable: "historical_member_intakes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_historical_member_intakes_OrganizationId_Status_CreatedAtUtc",
            schema: "core",
            table: "historical_member_intakes",
            columns: new[] { "OrganizationId", "Status", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_historical_member_intakes_TargetMemberId",
            schema: "core",
            table: "historical_member_intakes",
            column: "TargetMemberId");
        migrationBuilder.CreateIndex(
            name: "IX_historical_member_intakes_Rut",
            schema: "core",
            table: "historical_member_intakes",
            column: "Rut");
        migrationBuilder.CreateIndex(
            name: "IX_historical_member_intakes_InstitutionalNumber",
            schema: "core",
            table: "historical_member_intakes",
            column: "InstitutionalNumber");
        migrationBuilder.CreateIndex(
            name: "IX_historical_member_intake_offices_IntakeId_OfficeType_Period",
            schema: "core",
            table: "historical_member_intake_offices",
            columns: new[] { "IntakeId", "OfficeType", "Period" });
        migrationBuilder.CreateIndex(
            name: "IX_lodge_administrative_meetings_OrganizationId_MeetingDate",
            schema: "core",
            table: "lodge_administrative_meetings",
            columns: new[] { "OrganizationId", "MeetingDate" });
        migrationBuilder.CreateIndex(
            name: "IX_lodge_secretariat_records_OrganizationId_RecordType_EventDate",
            schema: "core",
            table: "lodge_secretariat_records",
            columns: new[] { "OrganizationId", "RecordType", "EventDate" });
        migrationBuilder.CreateIndex(
            name: "IX_lodge_secretariat_records_RecordType_SourceRecordId",
            schema: "core",
            table: "lodge_secretariat_records",
            columns: new[] { "RecordType", "SourceRecordId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_lodge_secretariat_records_Status_SubmittedAtUtc",
            schema: "core",
            table: "lodge_secretariat_records",
            columns: new[] { "Status", "SubmittedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "historical_member_intake_offices", schema: "core");
        migrationBuilder.DropTable(name: "lodge_administrative_meetings", schema: "core");
        migrationBuilder.DropTable(name: "lodge_secretariat_records", schema: "core");
        migrationBuilder.DropTable(name: "historical_member_intakes", schema: "core");

        migrationBuilder.DropColumn(name: "PlanchaKind", schema: "core", table: "secretariat_documents");
        migrationBuilder.DropColumn(name: "VirtualAccessReference", schema: "core", table: "lodge_meetings");
        migrationBuilder.DropColumn(name: "LocationReference", schema: "core", table: "lodge_meetings");
        migrationBuilder.DropColumn(name: "Modality", schema: "core", table: "lodge_meetings");
        migrationBuilder.DropColumn(name: "CeremonyType", schema: "core", table: "lodge_meetings");
        migrationBuilder.DropColumn(name: "CurrentDegree", schema: "core", table: "members");
        migrationBuilder.DropIndex(name: "IX_people_Rut", schema: "core", table: "people");
        migrationBuilder.DropColumn(name: "Rut", schema: "core", table: "people");

        migrationBuilder.AlterColumn<DateOnly>(
            name: "StartDate",
            schema: "core",
            table: "memberships",
            type: "date",
            nullable: false,
            defaultValue: default(DateOnly),
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateOnly>(
            name: "StartDate",
            schema: "core",
            table: "office_assignments",
            type: "date",
            nullable: false,
            defaultValue: default(DateOnly),
            oldClrType: typeof(DateOnly),
            oldType: "date",
            oldNullable: true);
    }
}
