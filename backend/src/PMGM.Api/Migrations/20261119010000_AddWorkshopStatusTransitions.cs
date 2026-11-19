using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

/// <summary>
/// #1035/#1044: historial de bajas y reactivaciones de Talleres. Solo operaciones nativas de EF (CreateTable / CreateIndex),
/// SIN <c>migrationBuilder.Sql</c>: tabla nueva, sin clave foránea ni cambios sobre tablas existentes (aditiva; compatible con
/// la API N-1, que no la conoce). El carácter append-only a nivel de BD lo agrega la migración MANUAL siguiente
/// (20261119010100_AddWorkshopStatusTransitionsAppendOnly). Sin backfill: los Talleres dados de baja antes de esta tabla se
/// resuelven desde su fotografía actual (WorkshopStatusHistory.Resolve), sin inventar fechas.
/// </summary>
[DbContext(typeof(PmgmDbContext))]
[Migration("20261119010000_AddWorkshopStatusTransitions")]
public sealed class AddWorkshopStatusTransitions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "workshop_status_transitions", schema: "core",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uuid", nullable: false),
                Sequence = t.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                SubjectRef = t.Column<Guid>(type: "uuid", nullable: false),
                PreviousStatus = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                NextStatus = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                EffectiveAtUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ActorSubject = t.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ReasonRef = t.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
            },
            constraints: t => t.PrimaryKey("PK_workshop_status_transitions", x => x.Id));
        migrationBuilder.CreateIndex("IX_workshop_status_transitions_SubjectRef_Sequence", schema: "core", table: "workshop_status_transitions", columns: new[] { "SubjectRef", "Sequence" });
        migrationBuilder.AddCheckConstraint(
            name: "CK_workshop_status_transitions_Status", schema: "core", table: "workshop_status_transitions",
            sql: "\"PreviousStatus\" IN ('active', 'retired') AND \"NextStatus\" IN ('active', 'retired') AND \"PreviousStatus\" <> \"NextStatus\"");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback conservador: el historial de bajas no se destruye; solo se retira la restricción agregada.
        migrationBuilder.DropCheckConstraint(name: "CK_workshop_status_transitions_Status", schema: "core", table: "workshop_status_transitions");
    }
}
