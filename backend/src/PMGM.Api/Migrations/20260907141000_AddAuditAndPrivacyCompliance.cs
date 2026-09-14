using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PMGM.Api.Data;

#nullable disable

namespace PMGM.Api.Migrations;

[DbContext(typeof(PmgmDbContext))]
[Migration("20260907141000_AddAuditAndPrivacyCompliance")]
public partial class AddAuditAndPrivacyCompliance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_events",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                EntityId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ActorDisplayName = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                Result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CorrelationId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_audit_events", x => x.Id));

        migrationBuilder.CreateTable(
            name: "data_processors",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Service = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Purpose = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                CountriesJson = table.Column<string>(type: "jsonb", nullable: false),
                DataCategoriesJson = table.Column<string>(type: "jsonb", nullable: false),
                SubjectCategoriesJson = table.Column<string>(type: "jsonb", nullable: true),
                SubprocessorsJson = table.Column<string>(type: "jsonb", nullable: true),
                AgreementReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                TransferMechanism = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                IncidentObligations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_data_processors", x => x.Id));

        migrationBuilder.CreateTable(
            name: "data_retention_policies",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                DataCategory = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                LegalBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RetentionDays = table.Column<int>(type: "integer", nullable: true),
                ExpirationEvent = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                ExpirationAction = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                AllowsLegalHold = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_data_retention_policies", x => x.Id));

        migrationBuilder.CreateTable(
            name: "privacy_security_incidents",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DetectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Source = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Nature = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                DataCategoriesJson = table.Column<string>(type: "jsonb", nullable: false),
                InvolvesSensitiveData = table.Column<bool>(type: "boolean", nullable: false),
                EstimatedSubjects = table.Column<int>(type: "integer", nullable: true),
                RiskLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ImmediateMeasures = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                NotifyAuthority = table.Column<bool>(type: "boolean", nullable: false),
                NotifySubjects = table.Column<bool>(type: "boolean", nullable: false),
                AuthorityNotifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                AuthorityReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SubjectsNotifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CorrectiveActions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_privacy_security_incidents", x => x.Id));

        migrationBuilder.CreateTable(
            name: "data_subject_requests",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                RequestType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Channel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                IdentityVerified = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                ExtensionUntil = table.Column<DateOnly>(type: "date", nullable: true),
                ResponsibleSubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                Resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Grounds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_data_subject_requests", x => x.Id);
                table.ForeignKey(
                    name: "FK_data_subject_requests_people_PersonId",
                    column: x => x.PersonId,
                    principalSchema: "core",
                    principalTable: "people",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "data_processing_activities",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Module = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Purpose = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                LawfulBasis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                DataCategoriesJson = table.Column<string>(type: "jsonb", nullable: false),
                SubjectCategoriesJson = table.Column<string>(type: "jsonb", nullable: false),
                ContainsSensitiveData = table.Column<bool>(type: "boolean", nullable: false),
                RecipientsJson = table.Column<string>(type: "jsonb", nullable: true),
                HasInternationalTransfer = table.Column<bool>(type: "boolean", nullable: false),
                RetentionPolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                InternalOwner = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_data_processing_activities", x => x.Id);
                table.ForeignKey(
                    name: "FK_data_processing_activities_data_retention_policies_RetentionPolicyId",
                    column: x => x.RetentionPolicyId,
                    principalSchema: "core",
                    principalTable: "data_retention_policies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "international_data_transfers",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DataProcessingActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                DataProcessorId = table.Column<Guid>(type: "uuid", nullable: true),
                DestinationCountry = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                LegalMechanism = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Safeguards = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_international_data_transfers", x => x.Id);
                table.ForeignKey(
                    name: "FK_international_data_transfers_data_processing_activities_DataProcessingActivityId",
                    column: x => x.DataProcessingActivityId,
                    principalSchema: "core",
                    principalTable: "data_processing_activities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_international_data_transfers_data_processors_DataProcessorId",
                    column: x => x.DataProcessorId,
                    principalSchema: "core",
                    principalTable: "data_processors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "privacy_impact_assessments",
            schema: "core",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DataProcessingActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                RiskLevel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                HighRisk = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AssessmentSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                Mitigations = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                ResidualRisk = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ApprovedBySubject = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EvidenceReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_privacy_impact_assessments", x => x.Id);
                table.ForeignKey(
                    name: "FK_privacy_impact_assessments_data_processing_activities_DataProcessingActivityId",
                    column: x => x.DataProcessingActivityId,
                    principalSchema: "core",
                    principalTable: "data_processing_activities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_OccurredAtUtc",
            schema: "core",
            table: "audit_events",
            column: "OccurredAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_EntityType_EntityId",
            schema: "core",
            table: "audit_events",
            columns: new[] { "EntityType", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_audit_events_CorrelationId",
            schema: "core",
            table: "audit_events",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_data_processors_Name_Service",
            schema: "core",
            table: "data_processors",
            columns: new[] { "Name", "Service" });

        migrationBuilder.CreateIndex(
            name: "IX_data_retention_policies_Code_EffectiveFrom",
            schema: "core",
            table: "data_retention_policies",
            columns: new[] { "Code", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_privacy_security_incidents_Status_DetectedAtUtc",
            schema: "core",
            table: "privacy_security_incidents",
            columns: new[] { "Status", "DetectedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_data_subject_requests_PersonId",
            schema: "core",
            table: "data_subject_requests",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_data_subject_requests_Status_DueDate",
            schema: "core",
            table: "data_subject_requests",
            columns: new[] { "Status", "DueDate" });

        migrationBuilder.CreateIndex(
            name: "IX_data_processing_activities_RetentionPolicyId",
            schema: "core",
            table: "data_processing_activities",
            column: "RetentionPolicyId");

        migrationBuilder.CreateIndex(
            name: "IX_data_processing_activities_Code_EffectiveFrom",
            schema: "core",
            table: "data_processing_activities",
            columns: new[] { "Code", "EffectiveFrom" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_international_data_transfers_DataProcessorId",
            schema: "core",
            table: "international_data_transfers",
            column: "DataProcessorId");

        migrationBuilder.CreateIndex(
            name: "IX_international_data_transfers_DataProcessingActivityId_DestinationCountry_EffectiveFrom",
            schema: "core",
            table: "international_data_transfers",
            columns: new[] { "DataProcessingActivityId", "DestinationCountry", "EffectiveFrom" });

        migrationBuilder.CreateIndex(
            name: "IX_privacy_impact_assessments_DataProcessingActivityId_CreatedAtUtc",
            schema: "core",
            table: "privacy_impact_assessments",
            columns: new[] { "DataProcessingActivityId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_events", schema: "core");
        migrationBuilder.DropTable(name: "data_subject_requests", schema: "core");
        migrationBuilder.DropTable(name: "international_data_transfers", schema: "core");
        migrationBuilder.DropTable(name: "privacy_impact_assessments", schema: "core");
        migrationBuilder.DropTable(name: "privacy_security_incidents", schema: "core");
        migrationBuilder.DropTable(name: "data_processors", schema: "core");
        migrationBuilder.DropTable(name: "data_processing_activities", schema: "core");
        migrationBuilder.DropTable(name: "data_retention_policies", schema: "core");
    }
}
