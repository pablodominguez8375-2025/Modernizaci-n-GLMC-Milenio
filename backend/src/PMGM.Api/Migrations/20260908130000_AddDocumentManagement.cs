using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260908130000_AddDocumentManagement")]
public partial class AddDocumentManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "document_collections",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_document_collections", x => x.Id);
                table.ForeignKey(
                    name: "FK_document_collections_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "institutional_documents",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                DocumentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Classification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AccessPolicy = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_institutional_documents", x => x.Id);
                table.ForeignKey(
                    name: "FK_institutional_documents_document_collections_CollectionId",
                    column: x => x.CollectionId,
                    principalSchema: "core",
                    principalTable: "document_collections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_institutional_documents_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "core",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "document_versions",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionNumber = table.Column<int>(type: "integer", nullable: false),
                OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ObjectKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ProcessingStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ScanReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_document_versions", x => x.Id);
                table.ForeignKey(
                    name: "FK_document_versions_institutional_documents_DocumentId",
                    column: x => x.DocumentId,
                    principalSchema: "core",
                    principalTable: "institutional_documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddForeignKey(
            name: "FK_institutional_documents_document_versions_PublishedVersionId",
            schema: "core",
            table: "institutional_documents",
            column: "PublishedVersionId",
            principalSchema: "core",
            principalTable: "document_versions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateIndex(
            name: "IX_document_collections_Code",
            schema: "core",
            table: "document_collections",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_document_collections_OrganizationId",
            schema: "core",
            table: "document_collections",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_document_collections_Scope_OrganizationId_Status",
            schema: "core",
            table: "document_collections",
            columns: new[] { "Scope", "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_documents_CollectionId_Status",
            schema: "core",
            table: "institutional_documents",
            columns: new[] { "CollectionId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_documents_OrganizationId_Status",
            schema: "core",
            table: "institutional_documents",
            columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_documents_PublishedVersionId",
            schema: "core",
            table: "institutional_documents",
            column: "PublishedVersionId");

        migrationBuilder.CreateIndex(
            name: "IX_document_versions_DocumentId_ProcessingStatus",
            schema: "core",
            table: "document_versions",
            columns: new[] { "DocumentId", "ProcessingStatus" });

        migrationBuilder.CreateIndex(
            name: "IX_document_versions_DocumentId_VersionNumber",
            schema: "core",
            table: "document_versions",
            columns: new[] { "DocumentId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_document_versions_ObjectKey",
            schema: "core",
            table: "document_versions",
            column: "ObjectKey",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_institutional_documents_document_versions_PublishedVersionId",
            schema: "core",
            table: "institutional_documents");

        migrationBuilder.DropTable(name: "document_versions", schema: "core");
        migrationBuilder.DropTable(name: "institutional_documents", schema: "core");
        migrationBuilder.DropTable(name: "document_collections", schema: "core");
    }
}
