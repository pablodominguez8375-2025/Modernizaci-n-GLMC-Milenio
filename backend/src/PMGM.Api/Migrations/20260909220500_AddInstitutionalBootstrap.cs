using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.Bootstrap;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(BootstrapDbContext))]
[Migration("20260909220500_AddInstitutionalBootstrap")]
public partial class AddInstitutionalBootstrap : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "institutional_bootstrap_applications",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PackageKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                PackageVersion = table.Column<int>(type: "integer", nullable: false),
                PayloadSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AppliedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SummaryJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_institutional_bootstrap_applications", x => x.Id));

        migrationBuilder.CreateTable(
            name: "security_profile_definitions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Description = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_security_profile_definitions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "office_definitions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                OrganizationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                AliasesJson = table.Column<string>(type: "jsonb", nullable: true),
                IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_office_definitions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_institutional_bootstrap_applications_PackageKey_PackageVersion",
            schema: "core",
            table: "institutional_bootstrap_applications",
            columns: new[] { "PackageKey", "PackageVersion" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_security_profile_definitions_Code",
            schema: "core",
            table: "security_profile_definitions",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_office_definitions_Code",
            schema: "core",
            table: "office_definitions",
            column: "Code",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "office_definitions", schema: "core");
        migrationBuilder.DropTable(name: "security_profile_definitions", schema: "core");
        migrationBuilder.DropTable(name: "institutional_bootstrap_applications", schema: "core");
    }
}
