using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(GrandArchiveDbContext))]
[Migration("20260909192100_AddGrandArchive")]
public partial class AddGrandArchive : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "grand_archive_records",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ArchiveCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                RecordType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                OriginatingBody = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                HistoricalPeriod = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                CreatedByDisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                WithdrawnAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                WithdrawnBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                WithdrawnByDisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                WithdrawalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_grand_archive_records", x => x.Id);
                table.ForeignKey(
                    name: "FK_grand_archive_records_institutional_documents_DocumentId",
                    column: x => x.DocumentId,
                    principalSchema: "core",
                    principalTable: "institutional_documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_grand_archive_records_document_versions_DocumentVersionId",
                    column: x => x.DocumentVersionId,
                    principalSchema: "core",
                    principalTable: "document_versions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_grand_archive_records_ArchiveCode",
            schema: "core",
            table: "grand_archive_records",
            column: "ArchiveCode",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_grand_archive_records_DocumentVersionId",
            schema: "core",
            table: "grand_archive_records",
            column: "DocumentVersionId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_grand_archive_records_DocumentId",
            schema: "core",
            table: "grand_archive_records",
            column: "DocumentId");
        migrationBuilder.CreateIndex(
            name: "IX_grand_archive_records_Status_DocumentDate",
            schema: "core",
            table: "grand_archive_records",
            columns: new[] { "Status", "DocumentDate" });
        migrationBuilder.CreateIndex(
            name: "IX_grand_archive_records_RecordType_Status",
            schema: "core",
            table: "grand_archive_records",
            columns: new[] { "RecordType", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "grand_archive_records", schema: "core");
}
