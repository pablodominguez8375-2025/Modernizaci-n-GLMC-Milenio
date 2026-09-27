using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260927100000_AddWorkPaperOwnership")]
public partial class AddWorkPaperOwnership : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "AuthorMemberId", schema: "core", table: "institutional_documents", type: "uuid", nullable: true);
        migrationBuilder.AddForeignKey(name: "FK_institutional_documents_members_AuthorMemberId", schema: "core", table: "institutional_documents", column: "AuthorMemberId", principalSchema: "core", principalTable: "members", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddColumn<int>(name: "AuthorEffectiveDegreeAtUpload", schema: "core", table: "document_versions", type: "integer", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SubmittedTitle", schema: "core", table: "document_versions", type: "character varying(240)", maxLength: 240, nullable: true);
        migrationBuilder.AddColumn<string>(name: "SubmittedShortDescription", schema: "core", table: "document_versions", type: "character varying(300)", maxLength: 300, nullable: true);
        migrationBuilder.CreateIndex(name: "IX_institutional_documents_DocumentType_AuthorMemberId", schema: "core", table: "institutional_documents", columns: new[] { "DocumentType", "AuthorMemberId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_institutional_documents_DocumentType_AuthorMemberId", schema: "core", table: "institutional_documents");
        migrationBuilder.DropForeignKey(name: "FK_institutional_documents_members_AuthorMemberId", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "AuthorMemberId", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "AuthorEffectiveDegreeAtUpload", schema: "core", table: "document_versions");
        migrationBuilder.DropColumn(name: "SubmittedTitle", schema: "core", table: "document_versions");
        migrationBuilder.DropColumn(name: "SubmittedShortDescription", schema: "core", table: "document_versions");
    }
}
