using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable
namespace PMGM.Api.Migrations;

/// <summary>
/// Published definitions are append-only, including for direct SQL writers.
/// The application must use a new consecutive row to change a form.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261012180000_AddFormSchemaVersions")]
public sealed class AddFormSchemaVersions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "form_schema_versions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ScopeKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                FormCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                SchemaJson = table.Column<string>(type: "text", nullable: false),
                PayloadSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                RecordedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_form_schema_versions", x => x.Id);
                table.CheckConstraint("CK_form_schema_versions_positive_version", "\"Version\" >= 1");
                table.CheckConstraint("CK_form_schema_versions_valid_scope",
                    "\"ScopeKey\" = 'order' OR \"ScopeKey\" ~ '^lodge:[0-9a-f]{32}$'");
                table.CheckConstraint("CK_form_schema_versions_valid_checksum",
                    "length(\"PayloadSha256\") = 64");
            });

        migrationBuilder.CreateIndex(
            name: "IX_form_schema_versions_ScopeKey_FormCode_Version",
            schema: "core", table: "form_schema_versions",
            columns: new[] { "ScopeKey", "FormCode", "Version" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_form_schema_versions_ScopeKey_FormCode_EffectiveFromUtc",
            schema: "core", table: "form_schema_versions",
            columns: new[] { "ScopeKey", "FormCode", "EffectiveFromUtc" });

        migrationBuilder.Sql("""
            CREATE FUNCTION core.prevent_form_schema_version_mutation()
            RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Published form schema versions are immutable'
                    USING ERRCODE = '55000';
            END;
            $$;
            CREATE TRIGGER trg_form_schema_versions_immutable
                BEFORE UPDATE OR DELETE ON core.form_schema_versions
                FOR EACH ROW EXECUTE FUNCTION core.prevent_form_schema_version_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "form_schema_versions", schema: "core");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS core.prevent_form_schema_version_mutation();");
    }
}
