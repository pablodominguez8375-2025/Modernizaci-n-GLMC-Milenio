using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907131000_AddMembershipHistory")]
public partial class AddMembershipHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_organizations_ParentOrganizationId",
            schema: "core",
            table: "organizations",
            column: "ParentOrganizationId");

        migrationBuilder.AddForeignKey(
            name: "FK_organizations_organizations_ParentOrganizationId",
            schema: "core",
            table: "organizations",
            column: "ParentOrganizationId",
            principalSchema: "core",
            principalTable: "organizations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "members",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionalNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_members", x => x.Id);
                table.ForeignKey(
                    name: "FK_members_people_PersonId",
                    column: x => x.PersonId,
                    principalSchema: "core",
                    principalTable: "people",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "memberships",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                MembershipType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EndReason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_memberships", x => x.Id);
                table.ForeignKey("FK_memberships_members_MemberId", x => x.MemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_memberships_organizations_OrganizationId", x => x.OrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "institutional_status_events",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_institutional_status_events", x => x.Id);
                table.ForeignKey("FK_institutional_status_events_members_MemberId", x => x.MemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_institutional_status_events_organizations_OrganizationId", x => x.OrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "degree_events",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Degree = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_degree_events", x => x.Id);
                table.ForeignKey("FK_degree_events_members_MemberId", x => x.MemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_degree_events_organizations_OrganizationId", x => x.OrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "office_assignments",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                OfficeType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Period = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_office_assignments", x => x.Id);
                table.ForeignKey("FK_office_assignments_members_MemberId", x => x.MemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_office_assignments_organizations_OrganizationId", x => x.OrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "member_transfers",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetMembershipId = table.Column<Guid>(type: "uuid", nullable: true),
                RequestedDate = table.Column<DateOnly>(type: "date", nullable: false),
                ProposedEffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                ApprovedEffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ExecutedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_transfers", x => x.Id);
                table.ForeignKey("FK_member_transfers_members_MemberId", x => x.MemberId, "core", "members", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_member_transfers_memberships_SourceMembershipId", x => x.SourceMembershipId, "core", "memberships", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_member_transfers_memberships_TargetMembershipId", x => x.TargetMembershipId, "core", "memberships", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_member_transfers_organizations_SourceOrganizationId", x => x.SourceOrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_member_transfers_organizations_TargetOrganizationId", x => x.TargetOrganizationId, "core", "organizations", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_members_PersonId", schema: "core", table: "members", column: "PersonId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_members_InstitutionalNumber", schema: "core", table: "members", column: "InstitutionalNumber", unique: true);
        migrationBuilder.CreateIndex(name: "IX_memberships_OrganizationId", schema: "core", table: "memberships", column: "OrganizationId");
        migrationBuilder.CreateIndex(name: "IX_memberships_MemberId_OrganizationId_StartDate", schema: "core", table: "memberships", columns: new[] { "MemberId", "OrganizationId", "StartDate" });
        migrationBuilder.CreateIndex(name: "IX_institutional_status_events_OrganizationId", schema: "core", table: "institutional_status_events", column: "OrganizationId");
        migrationBuilder.CreateIndex(name: "IX_institutional_status_events_MemberId_EffectiveDate", schema: "core", table: "institutional_status_events", columns: new[] { "MemberId", "EffectiveDate" });
        migrationBuilder.CreateIndex(name: "IX_degree_events_OrganizationId", schema: "core", table: "degree_events", column: "OrganizationId");
        migrationBuilder.CreateIndex(name: "IX_degree_events_MemberId_EffectiveDate", schema: "core", table: "degree_events", columns: new[] { "MemberId", "EffectiveDate" });
        migrationBuilder.CreateIndex(name: "IX_office_assignments_OrganizationId", schema: "core", table: "office_assignments", column: "OrganizationId");
        migrationBuilder.CreateIndex(name: "IX_office_assignments_MemberId_OrganizationId_StartDate", schema: "core", table: "office_assignments", columns: new[] { "MemberId", "OrganizationId", "StartDate" });
        migrationBuilder.CreateIndex(name: "IX_member_transfers_SourceMembershipId", schema: "core", table: "member_transfers", column: "SourceMembershipId");
        migrationBuilder.CreateIndex(name: "IX_member_transfers_TargetMembershipId", schema: "core", table: "member_transfers", column: "TargetMembershipId");
        migrationBuilder.CreateIndex(name: "IX_member_transfers_SourceOrganizationId", schema: "core", table: "member_transfers", column: "SourceOrganizationId");
        migrationBuilder.CreateIndex(name: "IX_member_transfers_TargetOrganizationId", schema: "core", table: "member_transfers", column: "TargetOrganizationId");
        migrationBuilder.CreateIndex(name: "IX_member_transfers_MemberId_RequestedDate", schema: "core", table: "member_transfers", columns: new[] { "MemberId", "RequestedDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "member_transfers", schema: "core");
        migrationBuilder.DropTable(name: "degree_events", schema: "core");
        migrationBuilder.DropTable(name: "institutional_status_events", schema: "core");
        migrationBuilder.DropTable(name: "office_assignments", schema: "core");
        migrationBuilder.DropTable(name: "memberships", schema: "core");
        migrationBuilder.DropTable(name: "members", schema: "core");

        migrationBuilder.DropForeignKey(
            name: "FK_organizations_organizations_ParentOrganizationId",
            schema: "core",
            table: "organizations");

        migrationBuilder.DropIndex(
            name: "IX_organizations_ParentOrganizationId",
            schema: "core",
            table: "organizations");
    }
}
