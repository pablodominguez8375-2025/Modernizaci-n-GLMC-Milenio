using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Modules.Admissions;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(AdmissionsDbContext))]
[Migration("20260911190500_EnsureAdmissionCaseCeremonyForeignKey")]
public partial class EnsureAdmissionCaseCeremonyForeignKey : Migration
{
    private const string ConstraintName = "FK_ceremony_requests_admission_cases_AdmissionCaseId";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            DO $pmgm$
            BEGIN
                IF to_regclass('core.ceremony_requests') IS NOT NULL
                   AND to_regclass('core.admission_cases') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM pg_constraint
                       WHERE conname = '{{ConstraintName}}'
                         AND conrelid = 'core.ceremony_requests'::regclass
                   )
                THEN
                    ALTER TABLE core.ceremony_requests
                    ADD CONSTRAINT "{{ConstraintName}}"
                    FOREIGN KEY ("AdmissionCaseId")
                    REFERENCES core.admission_cases ("Id")
                    ON DELETE RESTRICT;
                END IF;
            END
            $pmgm$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($$"""
            ALTER TABLE IF EXISTS core.ceremony_requests
            DROP CONSTRAINT IF EXISTS "{{ConstraintName}}";
            """);
    }
}
