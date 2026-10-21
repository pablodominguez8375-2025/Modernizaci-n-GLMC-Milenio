using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #613 — Origen y presentador son dos figuras distintas. Los expedientes
/// históricos conservan NULL: no se acredita una presentación por inferencia.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261021162000_AddCandidateWorkshopSponsor")]
public sealed class AddCandidateWorkshopSponsor : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<Guid>(
            name: "CandidateSponsorMemberId", schema: "core",
            table: "ceremony_requests", type: "uuid", nullable: true);
        m.CreateIndex(
            name: "IX_ceremony_requests_CandidateSponsorMemberId",
            schema: "core", table: "ceremony_requests",
            column: "CandidateSponsorMemberId");
        m.AddForeignKey(
            name: "FK_ceremony_requests_members_CandidateSponsorMemberId",
            schema: "core", table: "ceremony_requests",
            column: "CandidateSponsorMemberId",
            principalSchema: "core", principalTable: "members",
            principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropForeignKey(
            name: "FK_ceremony_requests_members_CandidateSponsorMemberId",
            schema: "core", table: "ceremony_requests");
        m.DropIndex(
            name: "IX_ceremony_requests_CandidateSponsorMemberId",
            schema: "core", table: "ceremony_requests");
        m.DropColumn(
            name: "CandidateSponsorMemberId",
            schema: "core", table: "ceremony_requests");
    }
}
