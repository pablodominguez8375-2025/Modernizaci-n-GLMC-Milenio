using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #588/#635 — perfil que publicó cada documento editorial (titularidad por
/// perfil/cargo). Nullable: los documentos históricos conservan su comportamiento.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261022160000_AddLibraryPublisherProfile")]
public sealed class AddLibraryPublisherProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(
            name: "PublisherProfile",
            schema: "core",
            table: "institutional_documents",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(
            name: "PublisherProfile",
            schema: "core",
            table: "institutional_documents");
}
