using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260911153000_AddLibraryCatalogMetadata")]
public partial class AddLibraryCatalogMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AuthorName",
            schema: "core",
            table: "institutional_documents",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AuthorLodgeName",
            schema: "core",
            table: "institutional_documents",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "DocumentDate",
            schema: "core",
            table: "institutional_documents",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Topic",
            schema: "core",
            table: "institutional_documents",
            type: "character varying(240)",
            maxLength: 240,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Edition",
            schema: "core",
            table: "institutional_documents",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ShortDescription",
            schema: "core",
            table: "institutional_documents",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AbstractText",
            schema: "core",
            table: "institutional_documents",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OfficialDocumentType",
            schema: "core",
            table: "institutional_documents",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_institutional_documents_DocumentType_MinimumDegreeRequired",
            schema: "core",
            table: "institutional_documents",
            columns: new[] { "DocumentType", "MinimumDegreeRequired" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_institutional_documents_DocumentType_MinimumDegreeRequired",
            schema: "core",
            table: "institutional_documents");

        migrationBuilder.DropColumn(name: "AuthorName", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "AuthorLodgeName", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "DocumentDate", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "Topic", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "Edition", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "ShortDescription", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "AbstractText", schema: "core", table: "institutional_documents");
        migrationBuilder.DropColumn(name: "OfficialDocumentType", schema: "core", table: "institutional_documents");
    }
}
