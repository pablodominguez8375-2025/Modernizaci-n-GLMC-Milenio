using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907155000_ExpandPrivacyIncidentWorkflow")]
public partial class ExpandPrivacyIncidentWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AssessmentCompletedAtUtc",
            schema: "core",
            table: "privacy_security_incidents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AssessmentSummary",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AuthorityDecision",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AuthorityDecisionAtUtc",
            schema: "core",
            table: "privacy_security_incidents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AuthorityDecisionReason",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AuthorityNotificationChannel",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SubjectsDecision",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SubjectsDecisionAtUtc",
            schema: "core",
            table: "privacy_security_incidents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SubjectsDecisionReason",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SubjectsNotificationChannel",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SubjectsNotificationReference",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ClosedAtUtc",
            schema: "core",
            table: "privacy_security_incidents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClosedBySubject",
            schema: "core",
            table: "privacy_security_incidents",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AssessmentCompletedAtUtc", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "AssessmentSummary", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "AuthorityDecision", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "AuthorityDecisionAtUtc", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "AuthorityDecisionReason", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "AuthorityNotificationChannel", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "SubjectsDecision", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "SubjectsDecisionAtUtc", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "SubjectsDecisionReason", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "SubjectsNotificationChannel", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "SubjectsNotificationReference", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "ClosedAtUtc", schema: "core", table: "privacy_security_incidents");
        migrationBuilder.DropColumn(name: "ClosedBySubject", schema: "core", table: "privacy_security_incidents");
    }
}
